// LogicGateOperator.cs
// 逻辑门算子 - Sprint 3 Task 3.2
// 支持：AND/OR/NOT/XOR/NAND/NOR
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 逻辑门算子
/// 
/// 功能：
/// - 双输入：AND, OR, XOR, NAND, NOR
/// - 单输入：NOT
/// - 输出：Result（Boolean）
/// 
/// 使用场景：
/// - 外观OK AND 尺寸OK AND 条码OK → 产品合格
/// - 多条件组合判定
/// </summary>
public class LogicGateOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LogicGate;

    public LogicGateOperator(ILogger<LogicGateOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("LogicGate 算子需要输入数据"));
        }

        // 获取参数
        var operation = GetStringParam(@operator, "Operation", "AND");

        // 获取输入值
        bool inputA = false;
        bool inputB = false;

        if (inputs.TryGetValue("InputA", out var valAObj) && valAObj != null)
        {
            inputA = ConvertToBool(valAObj);
        }

        if (inputs.TryGetValue("InputB", out var valBObj) && valBObj != null)
        {
            inputB = ConvertToBool(valBObj);
        }

        // 执行逻辑运算
        bool result = operation.ToUpper() switch
        {
            "AND" => inputA && inputB,
            "OR" => inputA || inputB,
            "NOT" => !inputA,
            "XOR" => inputA ^ inputB,
            "NAND" => !(inputA && inputB),
            "NOR" => !(inputA || inputB),
            _ => throw new ArgumentException($"不支持的操作: {operation}")
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

    /// <summary>
    /// 转换为布尔值
    /// </summary>
    private static bool ConvertToBool(object value)
    {
        if (value is bool b) return b;
        if (value is string s)
        {
            if (bool.TryParse(s, out var parsed)) return parsed;
            // 字符串非空为 true
            return !string.IsNullOrWhiteSpace(s);
        }
        if (value is int i) return i != 0;
        if (value is double d) return d != 0;
        if (value is float f) return f != 0;
        // 其他类型：非空为 true
        return value != null;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operation = GetStringParam(@operator, "Operation", "AND");

        var validOperations = new[] { "AND", "OR", "NOT", "XOR", "NAND", "NOR" };

        if (!validOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Operation 必须是以下之一: {string.Join(", ", validOperations)}");
        }

        return ValidationResult.Valid();
    }
}
