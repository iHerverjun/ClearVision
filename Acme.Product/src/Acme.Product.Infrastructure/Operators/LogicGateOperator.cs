using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "逻辑门",
    Description = "布尔逻辑运算 (AND, OR, NOT, XOR, NAND, NOR)",
    Category = "通用",
    IconName = "logic"
)]
[InputPort("InputA", "输入 A", PortDataType.Boolean, IsRequired = true)]
[InputPort("InputB", "输入 B", PortDataType.Boolean, IsRequired = false)]
[OutputPort("Result", "输出", PortDataType.Boolean)]
[OperatorParam("Operation", "逻辑操作", "enum", DefaultValue = "AND", Options = new[]
{
    "AND|AND",
    "OR|OR",
    "NOT|NOT",
    "XOR|XOR",
    "NAND|NAND",
    "NOR|NOR"
})]
public class LogicGateOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LogicGate;

    public LogicGateOperator(ILogger<LogicGateOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("LogicGate requires input data."));
        }

        var operation = GetStringParam(@operator, "Operation", "AND");
        if (!TryConvertToBool(inputs.TryGetValue("InputA", out var valAObj) ? valAObj : null, out var inputA))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("InputA must be a valid boolean-compatible value."));
        }

        var inputB = false;
        if (!operation.Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            if (!inputs.TryGetValue("InputB", out var valBObj) || !TryConvertToBool(valBObj, out inputB))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("InputB must be provided for binary logic operations."));
            }
        }

        var result = operation.ToUpperInvariant() switch
        {
            "AND" => inputA && inputB,
            "OR" => inputA || inputB,
            "NOT" => !inputA,
            "XOR" => inputA ^ inputB,
            "NAND" => !(inputA && inputB),
            "NOR" => !(inputA || inputB),
            _ => throw new ArgumentException($"Unsupported operation: {operation}")
        };

        Logger.LogDebug("[LogicGate] {InputA} {Operation} {InputB} = {Result}", inputA, operation, inputB, result);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Result", result },
            { "InputA", inputA },
            { "InputB", inputB },
            { "Operation", operation }
        }));
    }

    private static bool TryConvertToBool(object? value, out bool result)
    {
        result = false;
        if (value is null)
        {
            return false;
        }

        switch (value)
        {
            case bool b:
                result = b;
                return true;
            case int i:
                result = i != 0;
                return true;
            case long l:
                result = l != 0;
                return true;
            case double d:
                result = Math.Abs(d) > double.Epsilon;
                return true;
            case float f:
                result = Math.Abs(f) > float.Epsilon;
                return true;
            case string s:
                if (bool.TryParse(s, out var parsedBool))
                {
                    result = parsedBool;
                    return true;
                }

                if (int.TryParse(s, out var parsedInt))
                {
                    result = parsedInt != 0;
                    return true;
                }

                if (s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s.Equals("on", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (s.Equals("no", StringComparison.OrdinalIgnoreCase) || s.Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operation = GetStringParam(@operator, "Operation", "AND");
        var validOperations = new[] { "AND", "OR", "NOT", "XOR", "NAND", "NOR" };

        if (!validOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Operation must be one of: {string.Join(", ", validOperations)}");
        }

        return ValidationResult.Valid();
    }
}
