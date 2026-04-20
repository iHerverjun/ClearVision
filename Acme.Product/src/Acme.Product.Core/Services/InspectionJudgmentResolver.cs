using System.Collections;
using System.Text.Json;
using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Services;

public readonly record struct InspectionJudgmentEvaluation(
    InspectionStatus Status,
    string JudgmentSource,
    string StatusReason,
    bool MissingJudgmentSignal);

public static class InspectionJudgmentResolver
{
    private static readonly JudgmentBooleanSignal[] DirectBooleanSignals =
    [
        new("IsOk", PositiveMeansOk: true, "DerivedFromIsOk"),
        new("ConditionResult", PositiveMeansOk: true, "DerivedFromConditionResult"),
        new("Accepted", PositiveMeansOk: true, "DerivedFromAccepted"),
        new("VerificationPassed", PositiveMeansOk: true, "DerivedFromVerificationPassed"),
        new("IsMatch", PositiveMeansOk: true, "DerivedFromIsMatch"),
        new("IsMatched", PositiveMeansOk: true, "DerivedFromIsMatched"),
        new("HueValid", PositiveMeansOk: true, "DerivedFromHueValid"),
        new("IsSharp", PositiveMeansOk: true, "DerivedFromIsSharp"),
        new("IsAnomaly", PositiveMeansOk: false, "DerivedFromIsAnomaly")
    ];

    public static InspectionJudgmentEvaluation DetermineStatusFromFlowOutput(Dictionary<string, object>? outputData)
    {
        if (outputData == null)
        {
            return DetermineStatusFromFlowOutput(null, sourcePrefix: null, depth: 0);
        }

        var normalizedOutput = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in outputData)
        {
            normalizedOutput[key] = value;
        }

        return DetermineStatusFromFlowOutput(normalizedOutput, sourcePrefix: null, depth: 0);
    }

    private static InspectionJudgmentEvaluation DetermineStatusFromFlowOutput(
        IReadOnlyDictionary<string, object>? outputData,
        string? sourcePrefix,
        int depth)
    {
        if (outputData == null || outputData.Count == 0)
        {
            return MissingJudgmentSignal();
        }

        if (depth > 8)
        {
            return new InspectionJudgmentEvaluation(
                InspectionStatus.Error,
                ComposeJudgmentSource(sourcePrefix, "Depth"),
                "JudgmentTraversalDepthExceeded",
                false);
        }

        if (outputData.TryGetValue("JudgmentResult", out var judgmentResult))
        {
            if (!TryGetStringValue(judgmentResult, out var judgmentText))
            {
                return BuildInvalidTypeResult(ComposeJudgmentSource(sourcePrefix, "JudgmentResult"), "string", judgmentResult);
            }

            return ParseJudgmentResult(judgmentText!, ComposeJudgmentSource(sourcePrefix, "JudgmentResult"));
        }

        foreach (var signal in DirectBooleanSignals)
        {
            if (TryEvaluateBooleanSignal(outputData, signal, sourcePrefix, out var evaluation))
            {
                return evaluation;
            }
        }

        if (outputData.TryGetValue("Result", out var resultValue))
        {
            if (TryGetBoolValue(resultValue, out var resultBool))
            {
                return resultBool
                    ? new InspectionJudgmentEvaluation(
                        InspectionStatus.OK,
                        ComposeJudgmentSource(sourcePrefix, "Result"),
                        "DerivedFromResult",
                        false)
                    : new InspectionJudgmentEvaluation(
                        InspectionStatus.NG,
                        ComposeJudgmentSource(sourcePrefix, "Result"),
                        "DerivedFromResult",
                        false);
            }

            if (TryGetStringValue(resultValue, out var resultText) &&
                TryParseExactJudgmentKeyword(resultText!, out var resultStatus))
            {
                return new InspectionJudgmentEvaluation(
                    resultStatus,
                    ComposeJudgmentSource(sourcePrefix, "Result"),
                    "DerivedFromResultText",
                    false);
            }
        }

        if (outputData.TryGetValue("DefectCount", out var defectCountValue))
        {
            if (!TryGetIntValue(defectCountValue, out var defectCount))
            {
                return BuildInvalidTypeResult(ComposeJudgmentSource(sourcePrefix, "DefectCount"), "int", defectCountValue);
            }

            return defectCount > 0
                ? new InspectionJudgmentEvaluation(
                    InspectionStatus.NG,
                    ComposeJudgmentSource(sourcePrefix, "DefectCount"),
                    "DerivedFromDefectCount",
                    false)
                : new InspectionJudgmentEvaluation(
                    InspectionStatus.OK,
                    ComposeJudgmentSource(sourcePrefix, "DefectCount"),
                    "DerivedFromDefectCount",
                    false);
        }

        foreach (var (key, value) in outputData)
        {
            if (value == null)
            {
                continue;
            }

            if (!TryExtractNestedJudgmentPayload(value, out var nestedPayload))
            {
                continue;
            }

            var nestedEvaluation = DetermineStatusFromFlowOutput(
                nestedPayload,
                ComposeJudgmentSource(sourcePrefix, key),
                depth + 1);

            if (!nestedEvaluation.MissingJudgmentSignal)
            {
                return nestedEvaluation;
            }
        }

        return MissingJudgmentSignal();
    }

    private static InspectionJudgmentEvaluation ParseJudgmentResult(string judgmentText, string source)
    {
        var normalized = judgmentText.Trim();
        if (normalized.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            return new InspectionJudgmentEvaluation(InspectionStatus.Error, source, "DerivedFromJudgmentResult", false);
        }

        if (normalized.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Pass", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Passed", StringComparison.OrdinalIgnoreCase))
        {
            return new InspectionJudgmentEvaluation(InspectionStatus.OK, source, "DerivedFromJudgmentResult", false);
        }

        return new InspectionJudgmentEvaluation(InspectionStatus.NG, source, "DerivedFromJudgmentResult", false);
    }

    private static bool TryEvaluateBooleanSignal(
        IReadOnlyDictionary<string, object> outputData,
        JudgmentBooleanSignal signal,
        string? sourcePrefix,
        out InspectionJudgmentEvaluation evaluation)
    {
        evaluation = default;
        if (!outputData.TryGetValue(signal.FieldName, out var rawValue))
        {
            return false;
        }

        if (!TryGetBoolValue(rawValue, out var boolValue))
        {
            evaluation = BuildInvalidTypeResult(ComposeJudgmentSource(sourcePrefix, signal.FieldName), "bool", rawValue);
            return true;
        }

        var status = signal.PositiveMeansOk == boolValue ? InspectionStatus.OK : InspectionStatus.NG;
        evaluation = new InspectionJudgmentEvaluation(
            status,
            ComposeJudgmentSource(sourcePrefix, signal.FieldName),
            signal.StatusReason,
            false);
        return true;
    }

    private static bool TryExtractNestedJudgmentPayload(object value, out Dictionary<string, object> payload)
    {
        switch (value)
        {
            case Dictionary<string, object> dictionary:
                payload = new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);
                return true;
            case IReadOnlyDictionary<string, object> readOnlyDictionary:
                payload = new Dictionary<string, object>(readOnlyDictionary, StringComparer.OrdinalIgnoreCase);
                return true;
            case IDictionary legacyDictionary:
                payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in legacyDictionary)
                {
                    if (entry.Key is string key && entry.Value != null)
                    {
                        payload[key] = entry.Value;
                    }
                }

                return payload.Count > 0;
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                payload = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()) ??
                          new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return payload.Count > 0;
            default:
                payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return false;
        }
    }

    private static bool TryGetStringValue(object? value, out string? text)
    {
        switch (value)
        {
            case string s:
                text = s;
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                text = element.GetString();
                return true;
            default:
                text = null;
                return false;
        }
    }

    private static bool TryGetBoolValue(object? value, out bool boolValue)
    {
        switch (value)
        {
            case bool b:
                boolValue = b;
                return true;
            case JsonElement { ValueKind: JsonValueKind.True }:
                boolValue = true;
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                boolValue = false;
                return true;
            default:
                boolValue = false;
                return false;
        }
    }

    private static bool TryGetIntValue(object? value, out int intValue)
    {
        switch (value)
        {
            case int i:
                intValue = i;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                intValue = (int)l;
                return true;
            case double d when IsWholeNumberInIntRange(d):
                intValue = (int)d;
                return true;
            case float f when IsWholeNumberInIntRange(f):
                intValue = (int)f;
                return true;
            case decimal m when m >= int.MinValue && m <= int.MaxValue && decimal.Truncate(m) == m:
                intValue = (int)m;
                return true;
            case string s when int.TryParse(s, out var parsedString):
                intValue = parsedString;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var parsed):
                intValue = parsed;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDouble(out var parsedDouble) && IsWholeNumberInIntRange(parsedDouble):
                intValue = (int)parsedDouble;
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var parsedStringElement):
                intValue = parsedStringElement;
                return true;
            default:
                intValue = 0;
                return false;
        }
    }

    private static bool IsWholeNumberInIntRange(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               value >= int.MinValue &&
               value <= int.MaxValue &&
               Math.Truncate(value) == value;
    }

    private static bool TryParseExactJudgmentKeyword(string value, out InspectionStatus status)
    {
        var normalized = value.Trim();
        if (normalized.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("NG", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            status = normalized.Equals("OK", StringComparison.OrdinalIgnoreCase)
                ? InspectionStatus.OK
                : normalized.Equals("Error", StringComparison.OrdinalIgnoreCase)
                    ? InspectionStatus.Error
                    : InspectionStatus.NG;
            return true;
        }

        status = InspectionStatus.NotInspected;
        return false;
    }

    private static InspectionJudgmentEvaluation MissingJudgmentSignal()
    {
        return new InspectionJudgmentEvaluation(InspectionStatus.Error, "None", "MissingJudgmentSignal", true);
    }

    private static InspectionJudgmentEvaluation BuildInvalidTypeResult(
        string fieldName,
        string expectedType,
        object? actualValue)
    {
        return new InspectionJudgmentEvaluation(
            InspectionStatus.Error,
            fieldName,
            $"InvalidJudgmentType:{fieldName}:Expected={expectedType}:Actual={DescribeType(actualValue)}",
            false);
    }

    private static string ComposeJudgmentSource(string? prefix, string fieldName)
    {
        return string.IsNullOrWhiteSpace(prefix)
            ? fieldName
            : $"{prefix}.{fieldName}";
    }

    private static string DescribeType(object? value)
    {
        return value?.GetType().Name ?? "null";
    }

    private readonly record struct JudgmentBooleanSignal(
        string FieldName,
        bool PositiveMeansOk,
        string StatusReason);
}
