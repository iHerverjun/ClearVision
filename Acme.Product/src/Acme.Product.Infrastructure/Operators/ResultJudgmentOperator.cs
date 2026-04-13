using System.Globalization;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Result Judgment",
    Description = "Generic business judgment with numeric/string condition checks.",
    Category = "Flow Control",
    IconName = "result-judgment",
    Keywords = new[] { "judgment", "ok", "ng", "condition", "threshold" }
)]
[InputPort("Value", "Value", PortDataType.Any, IsRequired = false)]
[InputPort("Confidence", "Confidence", PortDataType.Float, IsRequired = false)]
[OutputPort("JudgmentResult", "Judgment Result", PortDataType.String)]
[OutputPort("IsOk", "Is OK", PortDataType.Boolean)]
[OutputPort("ConditionResult", "Condition Result", PortDataType.Boolean)]
[OutputPort("JudgmentValue", "Judgment Value", PortDataType.String)]
[OutputPort("Details", "Details", PortDataType.String)]
[OperatorParam("FieldName", "Field Name", "string", DefaultValue = "Value")]
[OperatorParam("Condition", "Condition", "enum", DefaultValue = "Equal", Options = new[]
{
    "Equal|Equal",
    "NotEqual|Not Equal",
    "GreaterThan|Greater Than",
    "LessThan|Less Than",
    "GreaterOrEqual|Greater Or Equal",
    "LessOrEqual|Less Or Equal",
    "Range|Range"
})]
[OperatorParam("ExpectValue", "Expected Value", "string", DefaultValue = "1")]
[OperatorParam("ExpectValueMin", "Expected Min", "string", DefaultValue = "")]
[OperatorParam("ExpectValueMax", "Expected Max", "string", DefaultValue = "")]
[OperatorParam("MinConfidence", "Min Confidence", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("NumericAbsTolerance", "Numeric Absolute Tolerance", "double", DefaultValue = 1e-4, Min = 0.0, Max = 1000000.0)]
[OperatorParam("NumericRelTolerance", "Numeric Relative Tolerance", "double", DefaultValue = 1e-6, Min = 0.0, Max = 1.0)]
public sealed class ResultJudgmentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ResultJudgment;

    public ResultJudgmentOperator(ILogger<ResultJudgmentOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var fieldName = GetStringParam(@operator, "FieldName", "Value");
        var condition = GetStringParam(@operator, "Condition", "Equal");
        var expectValue = GetStringParam(@operator, "ExpectValue", string.Empty);
        var expectValueMin = GetStringParam(@operator, "ExpectValueMin", string.Empty);
        var expectValueMax = GetStringParam(@operator, "ExpectValueMax", string.Empty);
        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0, 0.0, 1.0);
        var absTol = GetDoubleParam(@operator, "NumericAbsTolerance", 1e-4, 0.0, 1_000_000.0);
        var relTol = GetDoubleParam(@operator, "NumericRelTolerance", 1e-6, 0.0, 1.0);

        inputs ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var actualValue = ResolveActualValue(inputs, fieldName);

        if (TryParseDoubleInvariant(GetInputValue(inputs, "Confidence"), out var confidence)
            && confidence < minConfidence)
        {
            var lowConfidenceOutput = CreateOutput(false, "MinConfidenceGate", "Confidence below MinConfidence", actualValue);
            return Task.FromResult(OperatorExecutionOutput.Success(lowConfidenceOutput));
        }

        var (isOk, details) = EvaluateCondition(
            actualValue,
            condition,
            expectValue,
            expectValueMin,
            expectValueMax,
            absTol,
            relTol);

        var output = CreateOutput(isOk, condition, details, actualValue);
        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0);
        if (minConfidence is < 0 or > 1)
        {
            return ValidationResult.Invalid("MinConfidence must be within [0, 1].");
        }

        var absTol = GetDoubleParam(@operator, "NumericAbsTolerance", 1e-4);
        if (absTol < 0)
        {
            return ValidationResult.Invalid("NumericAbsTolerance must be >= 0.");
        }

        var relTol = GetDoubleParam(@operator, "NumericRelTolerance", 1e-6);
        if (relTol < 0 || relTol > 1)
        {
            return ValidationResult.Invalid("NumericRelTolerance must be within [0, 1].");
        }

        return ValidationResult.Valid();
    }

    private static Dictionary<string, object> CreateOutput(bool isOk, string condition, string details, object? actualValue)
    {
        return new Dictionary<string, object>
        {
            ["JudgmentResult"] = isOk ? "OK" : "NG",
            ["IsOk"] = isOk,
            ["ConditionResult"] = isOk,
            ["JudgmentValue"] = isOk ? "1" : "0",
            ["Details"] = details,
            ["Condition"] = condition,
            ["ActualValue"] = actualValue?.ToString() ?? string.Empty
        };
    }

    private static object? ResolveActualValue(Dictionary<string, object> inputs, string fieldName)
    {
        var byField = GetInputValue(inputs, fieldName);
        if (byField != null)
        {
            return byField;
        }

        return GetInputValue(inputs, "Value");
    }

    private static object? GetInputValue(Dictionary<string, object> inputs, string key)
    {
        foreach (var pair in inputs)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static (bool isOk, string details) EvaluateCondition(
        object? actualValue,
        string condition,
        string expectValue,
        string expectValueMin,
        string expectValueMax,
        double absTol,
        double relTol)
    {
        var actualText = actualValue?.ToString() ?? string.Empty;
        var actualIsNumeric = TryParseDoubleInvariant(actualText, out var actualNum);
        var expectIsNumeric = TryParseDoubleInvariant(expectValue, out var expectNum);
        var minIsNumeric = TryParseDoubleInvariant(expectValueMin, out var expectMin);
        var maxIsNumeric = TryParseDoubleInvariant(expectValueMax, out var expectMax);

        switch (condition.Trim().ToLowerInvariant())
        {
            case "equal":
                if (actualIsNumeric && expectIsNumeric)
                {
                    var ok = NearlyEqual(actualNum, expectNum, absTol, relTol);
                    return (ok, $"{actualNum} == {expectNum} => {ok}");
                }

                return (string.Equals(actualText, expectValue, StringComparison.Ordinal), $"{actualText} == {expectValue}");
            case "notequal":
                if (actualIsNumeric && expectIsNumeric)
                {
                    var ok = !NearlyEqual(actualNum, expectNum, absTol, relTol);
                    return (ok, $"{actualNum} != {expectNum} => {ok}");
                }

                return (!string.Equals(actualText, expectValue, StringComparison.Ordinal), $"{actualText} != {expectValue}");
            case "greaterthan":
                return (actualIsNumeric && expectIsNumeric && actualNum > expectNum, $"{actualNum} > {expectNum}");
            case "lessthan":
                return (actualIsNumeric && expectIsNumeric && actualNum < expectNum, $"{actualNum} < {expectNum}");
            case "greaterorequal":
                return (actualIsNumeric && expectIsNumeric && (actualNum > expectNum || NearlyEqual(actualNum, expectNum, absTol, relTol)), $"{actualNum} >= {expectNum}");
            case "lessorequal":
                return (actualIsNumeric && expectIsNumeric && (actualNum < expectNum || NearlyEqual(actualNum, expectNum, absTol, relTol)), $"{actualNum} <= {expectNum}");
            case "range":
                if (!(actualIsNumeric && minIsNumeric && maxIsNumeric))
                {
                    return (false, "Range requires numeric actual/min/max.");
                }

                var inRange = actualNum > expectMin || NearlyEqual(actualNum, expectMin, absTol, relTol);
                inRange &= actualNum < expectMax || NearlyEqual(actualNum, expectMax, absTol, relTol);
                return (inRange, $"{expectMin} <= {actualNum} <= {expectMax}");
            default:
                return (false, $"Unsupported condition: {condition}");
        }
    }

    private static bool NearlyEqual(double a, double b, double absTol, double relTol)
    {
        var diff = Math.Abs(a - b);
        if (diff <= absTol)
        {
            return true;
        }

        var scale = Math.Max(Math.Abs(a), Math.Abs(b));
        return diff <= scale * relTol;
    }

    private static bool TryParseDoubleInvariant(object? raw, out double value)
    {
        switch (raw)
        {
            case null:
                value = 0;
                return false;
            case double d:
                value = d;
                return double.IsFinite(value);
            case float f:
                value = f;
                return double.IsFinite(value);
            case decimal m:
                value = (double)m;
                return double.IsFinite(value);
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            default:
                return double.TryParse(
                    raw.ToString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out value);
        }
    }
}
