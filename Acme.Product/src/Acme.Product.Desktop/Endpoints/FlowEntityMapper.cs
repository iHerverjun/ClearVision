using System.Collections;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Acme.Product.Application.DTOs;

namespace Acme.Product.Desktop.Endpoints;

internal static class FlowEntityMapper
{
    private static readonly OperatorFactory OperatorFactory = new();

    public static OperatorFlow ToEntity(CanvasFlowDataDto flowData)
    {
        ArgumentNullException.ThrowIfNull(flowData);
        return BuildFlow(
            flowData.Id,
            string.IsNullOrWhiteSpace(flowData.Name) ? "PreviewFlow" : flowData.Name,
            flowData.Operators,
            flowData.Connections.Select(connection => new ConnectionShape(
                connection.SourceOperatorId,
                connection.SourcePortId,
                connection.TargetOperatorId,
                connection.TargetPortId)));
    }

    public static OperatorFlow ToEntity(FlowDataDto flowData)
    {
        ArgumentNullException.ThrowIfNull(flowData);

        if (flowData.Operators.Count > 0)
        {
            return BuildFlow(
                flowData.Id,
                string.IsNullOrWhiteSpace(flowData.Name) ? "AutoTuneFlow" : flowData.Name,
                flowData.Operators,
                flowData.Connections.Select(connection => new ConnectionShape(
                    connection.SourceOperatorId != Guid.Empty ? connection.SourceOperatorId : connection.SourceId,
                    connection.SourcePortId,
                    connection.TargetOperatorId != Guid.Empty ? connection.TargetOperatorId : connection.TargetId,
                    connection.TargetPortId)));
        }

        return BuildLegacyFlow(flowData);
    }

    public static OperatorFlow ToEntity(UpdateFlowRequest flowData, string flowName = "PreviewFlow", Guid? flowId = null)
    {
        ArgumentNullException.ThrowIfNull(flowData);
        return BuildFlow(
            flowId ?? Guid.Empty,
            string.IsNullOrWhiteSpace(flowName) ? "PreviewFlow" : flowName,
            flowData.Operators,
            flowData.Connections.Select(connection => new ConnectionShape(
                connection.SourceOperatorId,
                connection.SourcePortId,
                connection.TargetOperatorId,
                connection.TargetPortId)));
    }

    public static OperatorFlow ToPreviewEntity(UpdateFlowRequest flowData, Guid targetNodeId, string flowName = "PreviewFlow", Guid? flowId = null)
    {
        ArgumentNullException.ThrowIfNull(flowData);
        var filtered = FilterToUpstreamSubgraph(flowData, targetNodeId);
        return ToEntity(filtered, flowName, flowId);
    }

    public static OperatorFlow ToPreviewEntity(FlowDataDto flowData, Guid targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(flowData);
        var filtered = FilterToUpstreamSubgraph(flowData, targetNodeId);
        return ToEntity(filtered);
    }

    private static OperatorFlow BuildFlow(
        Guid flowId,
        string flowName,
        IEnumerable<CanvasOperatorDataDto> operators,
        IEnumerable<ConnectionShape> connections)
    {
        var flow = new OperatorFlow(flowName);
        if (flowId != Guid.Empty)
        {
            SetId(flow, flowId);
        }

        foreach (var operatorData in operators)
        {
            var type = OperatorTypeAliasResolver.Resolve(Enum.Parse<OperatorType>(operatorData.Type, ignoreCase: true));
            var @operator = OperatorFactory.CreateOperator(
                type,
                string.IsNullOrWhiteSpace(operatorData.Name) ? type.ToString() : operatorData.Name,
                operatorData.X,
                operatorData.Y);

            if (operatorData.Id != Guid.Empty)
            {
                SetId(@operator, operatorData.Id);
            }

            if (operatorData.InputPorts?.Count > 0)
            {
                @operator.InputPorts.Clear();
                foreach (var port in operatorData.InputPorts)
                {
                    @operator.LoadInputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        Enum.Parse<PortDataType>(port.DataType, ignoreCase: true),
                        port.IsRequired);
                }
            }

            if (operatorData.OutputPorts?.Count > 0)
            {
                @operator.OutputPorts.Clear();
                foreach (var port in operatorData.OutputPorts)
                {
                    @operator.LoadOutputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        Enum.Parse<PortDataType>(port.DataType, ignoreCase: true));
                }
            }

            foreach (var (name, value) in EnumerateCanvasParameters(operatorData.Parameters))
            {
                var existing = @operator.Parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.SetValue(value);
                    continue;
                }

                @operator.AddParameter(new Parameter(
                    Guid.NewGuid(),
                    name,
                    name,
                    string.Empty,
                    ResolveDataType(value),
                    value,
                    null,
                    null,
                    false,
                    null));
            }

            flow.AddOperator(@operator);
        }

        foreach (var connection in connections)
        {
            var sourceOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.SourceOperatorId);
            var targetOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.TargetOperatorId);
            if (sourceOperator == null || targetOperator == null)
            {
                continue;
            }

            if (!TryResolveConnectionPorts(
                    sourceOperator,
                    targetOperator,
                    connection.SourcePortId,
                    connection.TargetPortId,
                    out var sourcePortId,
                    out var targetPortId))
            {
                continue;
            }

            flow.AddConnection(new OperatorConnection(
                sourceOperator.Id,
                sourcePortId,
                targetOperator.Id,
                targetPortId));
        }

        return flow;
    }

    private static OperatorFlow BuildLegacyFlow(FlowDataDto flowData)
    {
        var flow = new OperatorFlow(string.IsNullOrWhiteSpace(flowData.Name) ? "AutoTuneFlow" : flowData.Name);
        if (flowData.Id != Guid.Empty)
        {
            SetId(flow, flowData.Id);
        }

        foreach (var node in flowData.Nodes)
        {
            var @operator = OperatorFactory.CreateOperator(
                node.Type,
                string.IsNullOrWhiteSpace(node.Name) ? node.Type.ToString() : node.Name,
                node.Position.X,
                node.Position.Y);

            if (node.Id != Guid.Empty)
            {
                SetId(@operator, node.Id);
            }

            foreach (var (name, value) in node.Parameters)
            {
                var parameter = @operator.Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

                if (parameter != null)
                {
                    parameter.SetValue(value);
                    continue;
                }

                @operator.AddParameter(new Parameter(
                    Guid.NewGuid(),
                    name,
                    name,
                    string.Empty,
                    ResolveDataType(value),
                    value,
                    null,
                    null,
                    false,
                    null));
            }

            flow.AddOperator(@operator);
        }

        foreach (var connection in flowData.Connections)
        {
            var sourceOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.SourceId);
            var targetOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.TargetId);
            if (sourceOperator == null || targetOperator == null)
            {
                continue;
            }

            if (!TryResolveConnectionPorts(
                    sourceOperator,
                    targetOperator,
                    connection.SourcePortId,
                    connection.TargetPortId,
                    out var sourcePortId,
                    out var targetPortId))
            {
                continue;
            }

            flow.AddConnection(new OperatorConnection(
                sourceOperator.Id,
                sourcePortId,
                targetOperator.Id,
                targetPortId));
        }

        return flow;
    }

    private static OperatorFlow BuildFlow(
        Guid flowId,
        string flowName,
        IEnumerable<OperatorDto> operators,
        IEnumerable<ConnectionShape> connections)
    {
        var flow = new OperatorFlow(flowName);
        if (flowId != Guid.Empty)
        {
            SetId(flow, flowId);
        }

        foreach (var operatorData in operators)
        {
            var type = OperatorTypeAliasResolver.Resolve(operatorData.Type);
            var @operator = OperatorFactory.CreateOperator(
                type,
                string.IsNullOrWhiteSpace(operatorData.Name) ? type.ToString() : operatorData.Name,
                operatorData.X,
                operatorData.Y);

            if (operatorData.Id != Guid.Empty)
            {
                SetId(@operator, operatorData.Id);
            }

            if (operatorData.InputPorts?.Count > 0)
            {
                @operator.InputPorts.Clear();
                foreach (var port in operatorData.InputPorts)
                {
                    @operator.LoadInputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        port.DataType,
                        port.IsRequired);
                }
            }

            if (operatorData.OutputPorts?.Count > 0)
            {
                @operator.OutputPorts.Clear();
                foreach (var port in operatorData.OutputPorts)
                {
                    @operator.LoadOutputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        port.DataType);
                }
            }

            if (operatorData.Parameters != null)
            {
                @operator.Parameters.Clear();
                foreach (var parameterData in operatorData.Parameters)
                {
                    var parameter = new Parameter(
                        parameterData.Id == Guid.Empty ? Guid.NewGuid() : parameterData.Id,
                        parameterData.Name,
                        string.IsNullOrWhiteSpace(parameterData.DisplayName) ? parameterData.Name : parameterData.DisplayName,
                        parameterData.Description ?? string.Empty,
                        string.IsNullOrWhiteSpace(parameterData.DataType)
                            ? ResolveDataType(parameterData.Value ?? parameterData.DefaultValue)
                            : parameterData.DataType,
                        parameterData.DefaultValue,
                        parameterData.MinValue,
                        parameterData.MaxValue,
                        parameterData.IsRequired,
                        parameterData.Options);

                    if (parameterData.Value != null)
                    {
                        parameter.SetValue(parameterData.Value);
                    }

                    @operator.AddParameter(parameter);
                }
            }

            flow.AddOperator(@operator);
        }

        foreach (var connection in connections)
        {
            var sourceOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.SourceOperatorId);
            var targetOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.TargetOperatorId);
            if (sourceOperator == null || targetOperator == null)
            {
                continue;
            }

            if (!TryResolveConnectionPorts(
                    sourceOperator,
                    targetOperator,
                    connection.SourcePortId,
                    connection.TargetPortId,
                    out var sourcePortId,
                    out var targetPortId))
            {
                continue;
            }

            flow.AddConnection(new OperatorConnection(
                sourceOperator.Id,
                sourcePortId,
                targetOperator.Id,
                targetPortId));
        }

        return flow;
    }

    private static UpdateFlowRequest FilterToUpstreamSubgraph(UpdateFlowRequest flowData, Guid targetNodeId)
    {
        var relevantIds = CollectRelevantOperatorIds(
            targetNodeId,
            flowData.Connections.Select(connection => (connection.SourceOperatorId, connection.TargetOperatorId)));

        return new UpdateFlowRequest
        {
            Operators = flowData.Operators
                .Where(@operator => relevantIds.Contains(@operator.Id))
                .ToList(),
            Connections = flowData.Connections
                .Where(connection =>
                    relevantIds.Contains(connection.SourceOperatorId) &&
                    relevantIds.Contains(connection.TargetOperatorId))
                .ToList()
        };
    }

    private static FlowDataDto FilterToUpstreamSubgraph(FlowDataDto flowData, Guid targetNodeId)
    {
        var relevantIds = CollectRelevantOperatorIds(
            targetNodeId,
            flowData.Connections.Select(connection => (ResolveSourceOperatorId(connection), ResolveTargetOperatorId(connection))));

        return new FlowDataDto
        {
            Id = flowData.Id,
            Name = flowData.Name,
            Operators = flowData.Operators
                .Where(@operator => relevantIds.Contains(@operator.Id))
                .ToList(),
            Nodes = flowData.Nodes
                .Where(node => relevantIds.Contains(node.Id))
                .ToList(),
            Connections = flowData.Connections
                .Where(connection =>
                    relevantIds.Contains(ResolveSourceOperatorId(connection)) &&
                    relevantIds.Contains(ResolveTargetOperatorId(connection)))
                .ToList()
        };
    }

    private static HashSet<Guid> CollectRelevantOperatorIds(
        Guid targetNodeId,
        IEnumerable<(Guid SourceOperatorId, Guid TargetOperatorId)> connections)
    {
        var incomingByTarget = connections
            .Where(connection => connection.SourceOperatorId != Guid.Empty && connection.TargetOperatorId != Guid.Empty)
            .GroupBy(connection => connection.TargetOperatorId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(connection => connection.SourceOperatorId).Distinct().ToList());

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(targetNodeId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (!incomingByTarget.TryGetValue(current, out var sources))
            {
                continue;
            }

            foreach (var source in sources)
            {
                stack.Push(source);
            }
        }

        return visited;
    }

    private static Guid ResolveSourceOperatorId(FlowConnectionDto connection)
    {
        return connection.SourceOperatorId != Guid.Empty
            ? connection.SourceOperatorId
            : connection.SourceId;
    }

    private static Guid ResolveTargetOperatorId(FlowConnectionDto connection)
    {
        return connection.TargetOperatorId != Guid.Empty
            ? connection.TargetOperatorId
            : connection.TargetId;
    }

    private static bool TryResolveConnectionPorts(
        Operator sourceOperator,
        Operator targetOperator,
        Guid preferredSourcePortId,
        Guid preferredTargetPortId,
        out Guid sourcePortId,
        out Guid targetPortId)
    {
        var sourcePort = preferredSourcePortId != Guid.Empty
            ? sourceOperator.OutputPorts.FirstOrDefault(port => port.Id == preferredSourcePortId)
            : null;
        var targetPort = preferredTargetPortId != Guid.Empty
            ? targetOperator.InputPorts.FirstOrDefault(port => port.Id == preferredTargetPortId)
            : null;

        if (sourcePort != null && targetPort != null && !IsCompatible(sourcePort.DataType, targetPort.DataType))
        {
            sourcePort = ResolveCompatibleOutputPort(sourceOperator, sourcePort, targetPort);
            targetPort = ResolveCompatibleInputPort(targetOperator, sourcePort, targetPort);
        }

        if (sourcePort == null && targetPort != null)
        {
            sourcePort = FindMatchingOutputPort(sourceOperator, targetPort);
        }

        if (targetPort == null && sourcePort != null)
        {
            targetPort = FindMatchingInputPort(targetOperator, sourcePort);
        }

        if (sourcePort == null || targetPort == null)
        {
            foreach (var candidateSource in sourceOperator.OutputPorts)
            {
                var candidateTarget = FindMatchingInputPort(targetOperator, candidateSource);
                if (candidateTarget == null)
                {
                    continue;
                }

                sourcePort = candidateSource;
                targetPort = candidateTarget;
                break;
            }
        }

        if (sourcePort == null || targetPort == null)
        {
            sourcePortId = Guid.Empty;
            targetPortId = Guid.Empty;
            return false;
        }

        sourcePortId = sourcePort.Id;
        targetPortId = targetPort.Id;
        return true;
    }

    private static Port? FindMatchingOutputPort(Operator sourceOperator, Port targetPort)
    {
        return sourceOperator.OutputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, targetPort.Name, StringComparison.OrdinalIgnoreCase) &&
                   IsCompatible(port.DataType, targetPort.DataType)) ??
               sourceOperator.OutputPorts.FirstOrDefault(port => IsCompatible(port.DataType, targetPort.DataType));
    }

    private static Port? FindMatchingInputPort(Operator targetOperator, Port sourcePort)
    {
        return targetOperator.InputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, sourcePort.Name, StringComparison.OrdinalIgnoreCase) &&
                   IsCompatible(sourcePort.DataType, port.DataType)) ??
               targetOperator.InputPorts.FirstOrDefault(port => IsCompatible(sourcePort.DataType, port.DataType));
    }

    private static Port? ResolveCompatibleOutputPort(Operator sourceOperator, Port? preferredSourcePort, Port targetPort)
    {
        var targetMatched = FindMatchingOutputPort(sourceOperator, targetPort);
        if (targetMatched != null)
        {
            return targetMatched;
        }

        if (preferredSourcePort == null)
        {
            return null;
        }

        return sourceOperator.OutputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, preferredSourcePort.Name, StringComparison.OrdinalIgnoreCase) &&
                   IsCompatible(port.DataType, targetPort.DataType));
    }

    private static Port? ResolveCompatibleInputPort(Operator targetOperator, Port? sourcePort, Port? preferredTargetPort)
    {
        if (sourcePort == null)
        {
            return preferredTargetPort;
        }

        var sourceMatched = FindMatchingInputPort(targetOperator, sourcePort);
        if (sourceMatched != null)
        {
            return sourceMatched;
        }

        if (preferredTargetPort == null)
        {
            return null;
        }

        return targetOperator.InputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, preferredTargetPort.Name, StringComparison.OrdinalIgnoreCase) &&
                   IsCompatible(sourcePort.DataType, port.DataType));
    }

    private static bool IsCompatible(PortDataType source, PortDataType target)
    {
        return source == target || source == PortDataType.Any || target == PortDataType.Any;
    }

    private static string ResolveDataType(object? value)
    {
        return value switch
        {
            null => "string",
            bool => "bool",
            byte or sbyte or short or ushort or int or uint or long or ulong => "int",
            float or double or decimal => "double",
            _ => "string"
        };
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateCanvasParameters(object? parameters)
    {
        switch (parameters)
        {
            case null:
                yield break;
            case IDictionary<string, object> typedDictionary:
                foreach (var (key, value) in typedDictionary)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        yield return new KeyValuePair<string, object?>(key, value);
                    }
                }
                yield break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        yield return new KeyValuePair<string, object?>(key!, entry.Value);
                    }
                }
                yield break;
            case JsonElement jsonElement:
                foreach (var pair in EnumerateCanvasParameters(jsonElement))
                {
                    yield return pair;
                }
                yield break;
            case IEnumerable enumerable when parameters is not string:
                foreach (var pair in EnumerateCanvasParameters(enumerable))
                {
                    yield return pair;
                }
                yield break;
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateCanvasParameters(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return new KeyValuePair<string, object?>(property.Name, ConvertJsonElementValue(property.Value));
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (TryExtractCanvasParameter(item, out var name, out var value))
            {
                yield return new KeyValuePair<string, object?>(name, value);
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateCanvasParameters(IEnumerable parameters)
    {
        foreach (var item in parameters)
        {
            if (item is JsonElement jsonElement)
            {
                if (TryExtractCanvasParameter(jsonElement, out var jsonName, out var jsonValue))
                {
                    yield return new KeyValuePair<string, object?>(jsonName, jsonValue);
                }

                continue;
            }

            if (TryExtractCanvasParameter(item, out var name, out var value))
            {
                yield return new KeyValuePair<string, object?>(name, value);
            }
        }
    }

    private static bool TryExtractCanvasParameter(JsonElement item, out string name, out object? value)
    {
        name = string.Empty;
        value = null;

        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? candidateName = null;
        JsonElement? candidateValue = null;
        JsonElement? candidateDefault = null;

        foreach (var property in item.EnumerateObject())
        {
            if (property.NameEquals("name") || property.NameEquals("Name"))
            {
                candidateName = property.Value.GetString();
            }
            else if (property.NameEquals("value") || property.NameEquals("Value"))
            {
                candidateValue = property.Value;
            }
            else if (property.NameEquals("defaultValue") || property.NameEquals("DefaultValue"))
            {
                candidateDefault = property.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        name = candidateName.Trim();
        value = candidateValue.HasValue
            ? ConvertJsonElementValue(candidateValue.Value)
            : candidateDefault.HasValue
                ? ConvertJsonElementValue(candidateDefault.Value)
                : null;
        return true;
    }

    private static bool TryExtractCanvasParameter(object? item, out string name, out object? value)
    {
        name = string.Empty;
        value = null;

        if (item == null)
        {
            return false;
        }

        if (item is IDictionary dictionary)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is string key)
                {
                    values[key] = entry.Value;
                }
            }

            if (!values.TryGetValue("name", out var nameValue) ||
                string.IsNullOrWhiteSpace(nameValue?.ToString()))
            {
                return false;
            }

            name = nameValue.ToString()!.Trim();
            value = values.TryGetValue("value", out var directValue)
                ? directValue
                : values.TryGetValue("defaultValue", out var defaultValue)
                    ? defaultValue
                    : null;
            return true;
        }

        var itemType = item.GetType();
        var nameProperty = itemType.GetProperty("Name") ?? itemType.GetProperty("name");
        var candidateName = nameProperty?.GetValue(item)?.ToString();
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        var valueProperty = itemType.GetProperty("Value") ?? itemType.GetProperty("value");
        var defaultValueProperty = itemType.GetProperty("DefaultValue") ?? itemType.GetProperty("defaultValue");

        name = candidateName.Trim();
        value = valueProperty?.GetValue(item) ?? defaultValueProperty?.GetValue(item);
        return true;
    }

    private static object? ConvertJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue)
                ? longValue
                : element.TryGetDouble(out var doubleValue)
                    ? doubleValue
                    : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static void SetId(object entity, Guid id)
    {
        entity.GetType().GetProperty("Id")?.SetValue(entity, id);
    }

    private readonly record struct ConnectionShape(
        Guid SourceOperatorId,
        Guid SourcePortId,
        Guid TargetOperatorId,
        Guid TargetPortId);
}
