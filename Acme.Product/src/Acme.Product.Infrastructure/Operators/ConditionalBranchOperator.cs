// ConditionalBranchOperator.cs
// 条件分支算子 - 流程控制（True// 功能实现False分支）
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 条件分支算子 - 流程控制（True/False分支）
/// </summary>
/// <remarks>
/// 注意：此算子需要FlowExecutionService支持条件分支路由
/// 输出数据会通过True或False端口传递，供后续算子使用
/// </remarks>
public class ConditionalBranchOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ConditionalBranch;

    public ConditionalBranchOperator(ILogger<ConditionalBranchOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Value", out var value) || value == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供判断值"));
        }

        // 获取参数
        var condition = GetStringParam(@operator, "Condition", "GreaterThan");
        var compareValueStr = GetStringParam(@operator, "CompareValue", "0");
        var fieldName = GetStringParam(@operator, "FieldName", "");

        // 如果指定了字段名，尝试从字典中获取字段值
        object? actualValue = value;
        if (!string.IsNullOrEmpty(fieldName) && value is Dictionary<string, object> dict && dict.ContainsKey(fieldName))
        {
            actualValue = dict[fieldName];
        }

        // 执行条件判断
        bool result = EvaluateCondition(actualValue, condition, compareValueStr);

        // 准备输出数据
        var outputData = new Dictionary<string, object>
        {
            { "Condition", condition },
            { "CompareValue", compareValueStr },
            { "ActualValue", actualValue is ImageWrapper ? "[ImageWrapper]" : (actualValue ?? "null") },
            { "Result", result },
            { "FieldName", fieldName }
        };

        // 根据结果将原始值输出到对应的端口
        // True端口：条件成立时的输出
        // False端口：条件不成立时的输出
        if (result)
        {
            outputData["True"] = PreserveOutputValue(value);
            outputData["False"] = null;
        }
        else
        {
            outputData["True"] = null;
            outputData["False"] = PreserveOutputValue(value);
        }

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private static object PreserveOutputValue(object value)
    {
        if (value is ImageWrapper wrapper)
            return wrapper.AddRef();
        return value;
    }

    private bool EvaluateCondition(object? actualValue, string condition, string compareValueStr)
    {
        // 尝试将值转换为数字进行比较
        double actualNum = 0;
        double compareNum = 0;
        bool isNumeric = double.TryParse(actualValue?.ToString(), out actualNum) && 
                         double.TryParse(compareValueStr, out compareNum);

        // 字符串值
        string actualStr = actualValue?.ToString() ?? "";
        string compareStr = compareValueStr;

        return condition.ToLower() switch
        {
            "greaterthan" => isNumeric && actualNum > compareNum,
            "lessthan" => isNumeric && actualNum < compareNum,
            "equal" => isNumeric ? actualNum == compareNum : actualStr == compareStr,
            "notequal" => isNumeric ? actualNum != compareNum : actualStr != compareStr,
            "contains" => actualStr.Contains(compareStr),
            "startswith" => actualStr.StartsWith(compareStr),
            "endswith" => actualStr.EndsWith(compareStr),
            _ => false
        };
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var condition = GetStringParam(@operator, "Condition", "GreaterThan");

        var validConditions = new[] { "GreaterThan", "LessThan", "Equal", "NotEqual", "Contains", "StartsWith", "EndsWith" };
        if (!validConditions.Contains(condition, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"条件必须是以下之一: {string.Join(", ", validConditions)}");
        }

        return ValidationResult.Valid();
    }
}
