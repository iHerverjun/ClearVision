using System.Text;
using System.Text.Json;
using Acme.Product.Core.DTOs;

namespace Acme.Product.Infrastructure.AI;

public sealed record AiPromptRequest(
    string Task,
    GenerateFlowMode Mode,
    string? AdditionalContext = null,
    string? TemplatePriority = null,
    string? AttachmentContext = null,
    string? SessionSummary = null,
    string? ReferenceFlowSummary = null,
    string? OutputRequirements = null);

public static class AiPromptComposer
{
    private const int MaxReferenceSummaryLength = 8000;

    public static string BuildUserPrompt(AiPromptRequest request)
    {
        var sb = new StringBuilder();

        AppendSection(sb, "Request", request.Task);
        AppendSection(sb, "Mode", BuildModeSection(request.Mode));
        AppendSection(sb, "AdditionalContext", request.AdditionalContext);
        AppendSection(sb, "TemplatePriority", request.TemplatePriority);
        AppendSection(sb, "AttachmentContext", request.AttachmentContext);
        AppendSection(sb, "SessionSummary", request.SessionSummary);
        AppendSection(sb, "ReferenceFlowSummary", request.ReferenceFlowSummary);
        AppendSection(sb, "OutputRequirements", request.OutputRequirements ?? BuildDefaultOutputRequirements(request.Mode));

        return sb.ToString().Trim();
    }

    public static string BuildReferenceFlowSummary(string? flowJson)
    {
        if (string.IsNullOrWhiteSpace(flowJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(flowJson);
            var root = doc.RootElement;
            var operatorsElement = TryGetPropertyIgnoreCase(root, "operators");
            var connectionsElement = TryGetPropertyIgnoreCase(root, "connections");

            if (operatorsElement is not { ValueKind: JsonValueKind.Array } operators)
                return string.Empty;

            var hasConnections = connectionsElement is { ValueKind: JsonValueKind.Array } connections &&
                                 connections.GetArrayLength() > 0;
            if (operators.GetArrayLength() == 0 && !hasConnections)
            {
                return string.Empty;
            }

            var isAiDraftFlow = operators.GetArrayLength() > 0 && LooksLikeAiDraftOperator(operators[0]);
            var summary = isAiDraftFlow
                ? BuildAiDraftReferenceSummary(operators, connectionsElement)
                : BuildCanvasReferenceSummary(operators, connectionsElement);

            if (summary.Length <= MaxReferenceSummaryLength)
                return summary;

            return summary[..MaxReferenceSummaryLength] + Environment.NewLine + "...<truncated>";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string BuildModeSection(GenerateFlowMode mode)
    {
        var guidance = mode switch
        {
            GenerateFlowMode.New => "Create a brand-new workflow from scratch.",
            GenerateFlowMode.Modify => "Apply incremental changes to the reference workflow and keep unrelated structure unchanged.",
            GenerateFlowMode.Explain => "Keep the workflow structure unchanged and improve only the explanation and rationale.",
            GenerateFlowMode.ReviewPendingParameters => "Review pending parameters and update only parameter values or minimal supporting metadata. Keep workflow structure stable.",
            _ => "Infer the most suitable generation mode from the request and available context."
        };

        return $"mode={mode.ToWireValue()}{Environment.NewLine}guidance={guidance}";
    }

    private static string BuildDefaultOutputRequirements(GenerateFlowMode mode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("- Return one complete JSON object only.");
        sb.AppendLine("- Do not add markdown code fences or extra explanation outside the JSON.");
        sb.AppendLine("- Keep operator ids, port names, and operator types aligned with the supported catalog from the system prompt.");

        if (mode == GenerateFlowMode.Modify)
        {
            sb.AppendLine("- Preserve already valid workflow structure unless the request explicitly requires a structural change.");
        }
        else if (mode == GenerateFlowMode.Explain)
        {
            sb.AppendLine("- Keep operators and connections unchanged. Update only explanation-related fields if needed.");
        }
        else if (mode == GenerateFlowMode.ReviewPendingParameters)
        {
            sb.AppendLine("- Keep workflow structure unchanged unless a missing supporting field is strictly required to complete parameter review.");
        }

        return sb.ToString().Trim();
    }

    private static void AppendSection(StringBuilder sb, string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        if (sb.Length > 0)
            sb.AppendLine();

        sb.AppendLine($"{title}:");
        sb.AppendLine(content.Trim());
    }

    private static bool LooksLikeAiDraftOperator(JsonElement element)
    {
        return TryGetPropertyIgnoreCase(element, "operatorType").HasValue ||
               TryGetPropertyIgnoreCase(element, "tempId").HasValue;
    }

    private static string BuildCanvasReferenceSummary(JsonElement operators, JsonElement? connectionsElement)
    {
        var summaries = new List<FlowOperatorSummary>();
        var outputPortNames = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var inputPortNames = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var op in operators.EnumerateArray())
        {
            index++;
            var operatorId = ReadString(op, "id") ?? $"op_{index}";
            var operatorType = ReadString(op, "type") ?? "Unknown";
            var displayName = ReadString(op, "name") ?? ReadString(op, "title") ?? operatorType;
            var parameters = ReadCanvasParameters(op);
            var inputs = ReadPortNames(op, "inputPorts");
            var outputs = ReadPortNames(op, "outputPorts");

            summaries.Add(new FlowOperatorSummary(operatorId, operatorType, displayName, parameters, inputs, outputs));
            inputPortNames[operatorId] = inputs.ToDictionary(name => name.Key, name => name.Value, StringComparer.OrdinalIgnoreCase);
            outputPortNames[operatorId] = outputs.ToDictionary(name => name.Key, name => name.Value, StringComparer.OrdinalIgnoreCase);
        }

        var connectionLines = new List<string>();
        if (connectionsElement is { ValueKind: JsonValueKind.Array } connections)
        {
            foreach (var conn in connections.EnumerateArray().Take(60))
            {
                var sourceOperatorId = ReadString(conn, "sourceOperatorId");
                var sourcePortId = ReadString(conn, "sourcePortId");
                var targetOperatorId = ReadString(conn, "targetOperatorId");
                var targetPortId = ReadString(conn, "targetPortId");
                if (string.IsNullOrWhiteSpace(sourceOperatorId) || string.IsNullOrWhiteSpace(targetOperatorId))
                    continue;

                var sourcePortName = ResolvePortName(outputPortNames, sourceOperatorId, sourcePortId);
                var targetPortName = ResolvePortName(inputPortNames, targetOperatorId, targetPortId);
                connectionLines.Add($"{sourceOperatorId}.{sourcePortName} -> {targetOperatorId}.{targetPortName}");
            }
        }

        return SerializeReferenceSummary(summaries, connectionLines);
    }

    private static string BuildAiDraftReferenceSummary(JsonElement operators, JsonElement? connectionsElement)
    {
        var summaries = new List<FlowOperatorSummary>();
        var index = 0;

        foreach (var op in operators.EnumerateArray())
        {
            index++;
            var operatorId = ReadString(op, "tempId") ?? $"op_{index}";
            var operatorType = ReadString(op, "operatorType") ?? "Unknown";
            var displayName = ReadString(op, "displayName") ?? operatorType;
            var parameters = ReadAiDraftParameters(op);

            summaries.Add(new FlowOperatorSummary(
                operatorId,
                operatorType,
                displayName,
                parameters,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        }

        var connectionLines = new List<string>();
        if (connectionsElement is { ValueKind: JsonValueKind.Array } connections)
        {
            foreach (var conn in connections.EnumerateArray().Take(60))
            {
                var sourceOperatorId = ReadString(conn, "sourceTempId");
                var sourcePortName = ReadString(conn, "sourcePortName") ?? "Output";
                var targetOperatorId = ReadString(conn, "targetTempId");
                var targetPortName = ReadString(conn, "targetPortName") ?? "Input";
                if (string.IsNullOrWhiteSpace(sourceOperatorId) || string.IsNullOrWhiteSpace(targetOperatorId))
                    continue;

                connectionLines.Add($"{sourceOperatorId}.{sourcePortName} -> {targetOperatorId}.{targetPortName}");
            }
        }

        return SerializeReferenceSummary(summaries, connectionLines);
    }

    private static string SerializeReferenceSummary(
        IReadOnlyList<FlowOperatorSummary> operators,
        IReadOnlyList<string> connections)
    {
        if (operators.Count == 0 && connections.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"operatorCount={operators.Count}");
        sb.AppendLine("operators:");
        foreach (var op in operators.Take(40))
        {
            var parameterText = op.Parameters.Count == 0
                ? "{}"
                : "{" + string.Join(", ", op.Parameters.Select(item => $"{item.Key}={item.Value}")) + "}";
            var inputText = op.Inputs.Count == 0
                ? "[]"
                : "[" + string.Join(", ", op.Inputs.Values) + "]";
            var outputText = op.Outputs.Count == 0
                ? "[]"
                : "[" + string.Join(", ", op.Outputs.Values) + "]";
            sb.AppendLine($"- operatorId={op.OperatorId} | operatorType={op.OperatorType} | displayName={op.DisplayName} | parameters={parameterText} | inputs={inputText} | outputs={outputText}");
        }

        sb.AppendLine($"connectionCount={connections.Count}");
        sb.AppendLine("connections:");
        if (connections.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var connection in connections)
            {
                sb.AppendLine($"- {connection}");
            }
        }

        return sb.ToString().Trim();
    }

    private static Dictionary<string, string> ReadCanvasParameters(JsonElement operatorElement)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parametersElement = TryGetPropertyIgnoreCase(operatorElement, "parameters");
        if (!parametersElement.HasValue)
            return parameters;

        if (parametersElement.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in parametersElement.Value.EnumerateArray().Take(12))
            {
                var name = ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var value = TryGetPropertyIgnoreCase(item, "value");
                parameters[name] = FormatJsonValue(value ?? default);
            }

            return parameters;
        }

        if (parametersElement.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in parametersElement.Value.EnumerateObject().Take(12))
            {
                parameters[property.Name] = FormatJsonValue(property.Value);
            }
        }

        return parameters;
    }

    private static Dictionary<string, string> ReadAiDraftParameters(JsonElement operatorElement)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parametersElement = TryGetPropertyIgnoreCase(operatorElement, "parameters");
        if (parametersElement is not { ValueKind: JsonValueKind.Object } parameterObject)
            return parameters;

        foreach (var property in parameterObject.EnumerateObject().Take(12))
        {
            parameters[property.Name] = FormatJsonValue(property.Value);
        }

        return parameters;
    }

    private static Dictionary<string, string> ReadPortNames(JsonElement operatorElement, string propertyName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var portsElement = TryGetPropertyIgnoreCase(operatorElement, propertyName);
        if (portsElement is not { ValueKind: JsonValueKind.Array } ports)
            return result;

        foreach (var port in ports.EnumerateArray().Take(20))
        {
            var portId = ReadString(port, "id");
            var portName = ReadString(port, "name");
            if (string.IsNullOrWhiteSpace(portId) || string.IsNullOrWhiteSpace(portName))
                continue;

            result[portId] = portName;
        }

        return result;
    }

    private static string ResolvePortName(
        IReadOnlyDictionary<string, Dictionary<string, string>> portMaps,
        string operatorId,
        string? portId)
    {
        if (!string.IsNullOrWhiteSpace(portId) &&
            portMaps.TryGetValue(operatorId, out var ports) &&
            ports.TryGetValue(portId, out var portName) &&
            !string.IsNullOrWhiteSpace(portName))
        {
            return portName;
        }

        return string.IsNullOrWhiteSpace(portId) ? "UnknownPort" : portId;
    }

    private static JsonElement? TryGetPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var value))
            return value;

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(pascalName, out value))
            return value;

        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) ||
                property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        var value = TryGetPropertyIgnoreCase(element, propertyName);
        if (value is not { } actualValue)
            return null;

        return actualValue.ValueKind switch
        {
            JsonValueKind.String => actualValue.GetString(),
            JsonValueKind.Number => actualValue.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => actualValue.GetRawText()
        };
    }

    private static string FormatJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined => string.Empty,
            JsonValueKind.Null => "null",
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => "[" + string.Join(", ", element.EnumerateArray().Take(6).Select(FormatJsonValue)) + "]",
            JsonValueKind.Object => "{...}",
            _ => element.GetRawText()
        };
    }

    private sealed record FlowOperatorSummary(
        string OperatorId,
        string OperatorType,
        string DisplayName,
        IReadOnlyDictionary<string, string> Parameters,
        IReadOnlyDictionary<string, string> Inputs,
        IReadOnlyDictionary<string, string> Outputs);
}
