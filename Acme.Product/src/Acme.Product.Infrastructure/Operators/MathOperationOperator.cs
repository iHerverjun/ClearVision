// MathOperationOperator.cs
// 数值计算算子 - Sprint 3 Task 3.1
// 支持：Add/Subtract/Multiply/Divide/Abs/Min/Max/Power/Sqrt/Round/Modulo
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 数值计算算子
/// 
/// 功能：
/// - 双操作数：Add, Subtract, Multiply, Divide, Min, Max, Power, Modulo
/// - 单操作数：Abs, Sqrt, Round
/// - 输出：Result（Float）、IsPositive（Boolean）
/// 
/// 使用场景：
/// - 圆A.Radius → Subtract.ValueA，圆B.Radius → Subtract.ValueB → Abs → ConditionalBranch
/// - 尺寸公差计算
/// </summary>
public class MathOperationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MathOperation;

    public MathOperationOperator(ILogger<MathOperationOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("MathOperation 算子需要输入数据"));
        }

        // 获取参数
        var operation = GetStringParam(@operator, "Operation", "Add");
        
        // 获取输入值
        double valueA = 0;
        double valueB = 0;
        
        if (inputs.TryGetValue("ValueA", out var valAObj) && valAObj != null)
        {
            double.TryParse(valAObj.ToString(), out valueA);
        }
        
        if (inputs.TryGetValue("ValueB", out var valBObj) && valBObj != null)
        {
            double.TryParse(valBObj.ToString(), out valueB);
        }

        // 执行计算
        double result;
        try
        {
            result = operation.ToLower() switch
            {
                "add" => valueA + valueB,
                "subtract" => valueA - valueB,
                "multiply" => valueA * valueB,
                "divide" => valueB != 0 ? valueA / valueB : throw new DivideByZeroException("除数不能为零"),
                "abs" => Math.Abs(valueA),
                "min" => Math.Min(valueA, valueB),
                "max" => Math.Max(valueA, valueB),
                "power" => Math.Pow(valueA, valueB),
                "sqrt" => valueA >= 0 ? Math.Sqrt(valueA) : throw new ArgumentException("负数不能开平方根"),
                "round" => Math.Round(valueA),
                "modulo" => valueB != 0 ? valueA % valueB : throw new DivideByZeroException("模运算除数不能为零"),
                _ => throw new ArgumentException($"不支持的操作: {operation}")
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MathOperation] 计算失败: {Operation}({ValueA}, {ValueB})", operation, valueA, valueB);
            return Task.FromResult(OperatorExecutionOutput.Failure($"计算失败: {ex.Message}"));
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
            return ValidationResult.Invalid($"Operation 必须是以下之一: {string.Join(", ", validOperations)}");
        }

        return ValidationResult.Valid();
    }
}
