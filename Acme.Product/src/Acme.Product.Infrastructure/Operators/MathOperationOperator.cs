using System.Globalization;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "数值计算",
    Description = "支持加减乘除、取绝对值、开方等常用运算",
    Category = "数据处理",
    IconName = "calc",
    Keywords = new[] { "计算", "数学", "加减乘除", "数值", "判断大小", "运算", "Math", "Calculate", "Add", "Subtract", "Multiply", "Divide" }
)]
[InputPort("ValueA", "数值 A", PortDataType.Float, IsRequired = true)]
[InputPort("ValueB", "数值 B", PortDataType.Float, IsRequired = false)]
[OutputPort("Result", "结果", PortDataType.Float)]
[OutputPort("IsPositive", "大于零", PortDataType.Boolean)]
[OperatorParam("Operation", "运算类型", "enum", DefaultValue = "Add", Options = new[] { "Add|加 (+)", "Subtract|减 (-)", "Multiply|乘 (×)", "Divide|除 (÷)", "Abs|绝对值 (Abs)", "Min|取小 (Min)", "Max|取大 (Max)", "Power|幂运算 (Power)", "Sqrt|平方根 (Sqrt)", "Round|取整 (Round)", "Modulo|取余 (Modulo)" })]
public class MathOperationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MathOperation;

    public MathOperationOperator(ILogger<MathOperationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("MathOperation requires input data."));
        }

        var operation = GetStringParam(@operator, "Operation", "Add").Trim();
        var requiresSecondOperand = RequiresSecondOperand(operation);

        if (!TryGetRequiredFiniteInputDouble(inputs, "ValueA", out var valueA, out var valueAError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(valueAError));
        }

        var valueB = 0.0;
        if (requiresSecondOperand &&
            !TryGetRequiredFiniteInputDouble(inputs, "ValueB", out valueB, out var valueBError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(valueBError));
        }

        double result;
        try
        {
            result = operation.ToLowerInvariant() switch
            {
                "add" => valueA + valueB,
                "subtract" => valueA - valueB,
                "multiply" => valueA * valueB,
                "divide" => valueB != 0 ? valueA / valueB : throw new DivideByZeroException("Divisor cannot be zero."),
                "abs" => Math.Abs(valueA),
                "min" => Math.Min(valueA, valueB),
                "max" => Math.Max(valueA, valueB),
                "power" => Math.Pow(valueA, valueB),
                "sqrt" => valueA >= 0 ? Math.Sqrt(valueA) : throw new ArgumentException("Cannot calculate sqrt for a negative number."),
                "round" => Math.Round(valueA),
                "modulo" => valueB != 0 ? valueA % valueB : throw new DivideByZeroException("Modulo divisor cannot be zero."),
                _ => throw new ArgumentException($"Unsupported operation: {operation}")
            };

            if (!double.IsFinite(result))
            {
                throw new ArithmeticException("Computation result must be a finite number.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MathOperation] Computation failed: {Operation}({ValueA}, {ValueB})", operation, valueA, valueB);
            return Task.FromResult(OperatorExecutionOutput.Failure($"Computation failed: {ex.Message}"));
        }

        Logger.LogDebug("[MathOperation] {ValueA} {Operation} {ValueB} = {Result}", valueA, operation, valueB, result);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Result", result },
            { "ResultFloat", (float)result },
            { "ResultInt", (int)result },
            { "IsPositive", result > 0 },
            { "IsZero", result == 0 },
            { "IsNegative", result < 0 },
            { "InputA", valueA },
            { "InputB", valueB },
            { "Operation", operation }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operation = GetStringParam(@operator, "Operation", "Add");
        var validOperations = new[]
        {
            "Add", "Subtract", "Multiply", "Divide",
            "Abs", "Min", "Max", "Power", "Sqrt", "Round", "Modulo"
        };

        if (!validOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Operation must be one of: {string.Join(", ", validOperations)}");
        }

        return ValidationResult.Valid();
    }

    private static bool RequiresSecondOperand(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "add" => true,
            "subtract" => true,
            "multiply" => true,
            "divide" => true,
            "min" => true,
            "max" => true,
            "power" => true,
            "modulo" => true,
            _ => false
        };
    }

    private static bool TryGetRequiredFiniteInputDouble(
        Dictionary<string, object> inputs,
        string key,
        out double value,
        out string errorMessage)
    {
        value = 0;
        errorMessage = string.Empty;

        if (!inputs.TryGetValue(key, out var rawValue) || rawValue == null)
        {
            errorMessage = $"Input '{key}' is required.";
            return false;
        }

        if (!TryConvertToDouble(rawValue, out value))
        {
            errorMessage = $"Input '{key}' must be a valid number.";
            return false;
        }

        if (!double.IsFinite(value))
        {
            errorMessage = $"Input '{key}' must be a finite number.";
            return false;
        }

        return true;
    }

    private static bool TryConvertToDouble(object raw, out double value)
    {
        value = 0;

        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case byte b:
                value = b;
                return true;
            case sbyte sb:
                value = sb;
                return true;
            case short s:
                value = s;
                return true;
            case ushort us:
                value = us;
                return true;
            case int i:
                value = i;
                return true;
            case uint ui:
                value = ui;
                return true;
            case long l:
                value = l;
                return true;
            case ulong ul:
                value = ul;
                return true;
            case decimal m:
                value = (double)m;
                return true;
            case string text:
                return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
            case IFormattable formattable:
                return double.TryParse(formattable.ToString(null, CultureInfo.InvariantCulture), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
            default:
                return double.TryParse(raw.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }
    }
}
