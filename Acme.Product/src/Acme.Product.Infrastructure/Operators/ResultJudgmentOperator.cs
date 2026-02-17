using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 结果判定算子 - 通用判定逻辑（数量/范围/阈值）
/// </summary>/// <remarks>/// 支持多种判定条件：
/// - Equal: 等于期望值
/// - GreaterThan: 大于期望值
/// - LessThan: 小于期望值
/// - Range: 在指定范围内 [Min, Max]
/// - GreaterOrEqual: 大于等于期望值
/// - LessOrEqual: 小于等于期望值
/// </remarks>
public class ResultJudgmentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ResultJudgment;

    public ResultJudgmentOperator(ILogger<ResultJudgmentOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var fieldName = GetStringParam(@operator, "FieldName", "Value");
        var condition = GetStringParam(@operator, "Condition", "Equal");
        var expectValue = GetStringParam(@operator, "ExpectValue", "");
        var expectValueMin = GetStringParam(@operator, "ExpectValueMin", "");
        var expectValueMax = GetStringParam(@operator, "ExpectValueMax", "");
        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0, 0.0, 1.0);
        var okOutputValue = GetStringParam(@operator, "OkOutputValue", "1");
        var ngOutputValue = GetStringParam(@operator, "NgOutputValue", "0");

        // 从输入中获取实际值
        object? actualValueObj = null;
        if (inputs != null && inputs.TryGetValue(fieldName, out var val) && val != null)
        {
            actualValueObj = val;
            Logger.LogInformation("[ResultJudgment] 从字段 '{FieldName}' 获取到值: {Value} (类型: {Type})", fieldName, actualValueObj, actualValueObj.GetType().Name);
        }
        else if (inputs != null && inputs.TryGetValue("Value", out var fallbackVal) && fallbackVal != null)
        {
            actualValueObj = fallbackVal;
            Logger.LogInformation("[ResultJudgment] 字段 '{FieldName}' 未找到，从 'Value' 获取到值: {Value} (类型: {Type})", fieldName, actualValueObj, actualValueObj.GetType().Name);
        }
        else
        {
            return Task.FromResult(CreateNgOutput($"未找到判定字段: {fieldName}", ngOutputValue));
        }

        Logger.LogInformation("[ResultJudgment] 判定参数: 条件={Condition}, 期望值={ExpectValue}", condition, expectValue);

        // 检查置信度（如果输入中有Confidence字段）
        if (inputs.TryGetValue("Confidence", out var confidenceObj) && confidenceObj is double confidence)
        {
            if (confidence < minConfidence)
            {
                return Task.FromResult(CreateNgOutput($"置信度 {confidence:F2} 低于最小阈值 {minConfidence:F2}", ngOutputValue, actualValueObj));
            }
        }

        // 执行判定
        var (isOk, details) = EvaluateCondition(actualValueObj, condition, expectValue, expectValueMin, expectValueMax);

        // 准备输出
        var outputValue = isOk ? okOutputValue : ngOutputValue;
        var judgmentResult = isOk ? "OK" : "NG";

        var outputData = new Dictionary<string, object>
        {
            { "JudgmentResult", judgmentResult },
            { "JudgmentValue", outputValue },
            { "ActualValue", actualValueObj },
            { "FieldName", fieldName },
            { "Condition", condition },
            { "ExpectValue", expectValue },
            { "Details", details },
            { "IsOk", isOk }
        };

        // 同时输出数值类型的判定值（便于PLC写入）
        if (int.TryParse(outputValue, out var intValue))
        {
            outputData["JudgmentValueInt"] = intValue;
        }

        Logger.LogInformation(
            "[ResultJudgment] 判定完成: {FieldName}={ActualValue}, 条件={Condition}, 结果={JudgmentResult}",
            fieldName, actualValueObj, condition, judgmentResult);

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private (bool isOk, string details) EvaluateCondition(
        object? actualValue,
        string condition,
        string expectValue,
        string expectValueMin,
        string expectValueMax)
    {
        var actualStr = actualValue?.ToString() ?? "";

        // 尝试数值比较
        bool isNumeric = double.TryParse(actualStr, out var actualNum);
        bool expectIsNumeric = double.TryParse(expectValue, out var expectNum);
        bool minIsNumeric = double.TryParse(expectValueMin, out var expectMin);
        bool maxIsNumeric = double.TryParse(expectValueMax, out var expectMax);

        switch (condition.ToLower())
        {
            case "equal":
                if (isNumeric && expectIsNumeric)
                {
                    var isOk = Math.Abs(actualNum - expectNum) < 0.0001;
                    return (isOk, $"数值比较: {actualNum} == {expectNum} ? {isOk}");
                }
                else
                {
                    var isOk = actualStr == expectValue;
                    return (isOk, $"字符串比较: '{actualStr}' == '{expectValue}' ? {isOk}");
                }

            case "greaterthan":
                if (isNumeric && expectIsNumeric)
                {
                    var isOk = actualNum > expectNum;
                    return (isOk, $"数值比较: {actualNum} > {expectNum} ? {isOk}");
                }
                return (false, "GreaterThan需要数值类型");

            case "lessthan":
                if (isNumeric && expectIsNumeric)
                {
                    var isOk = actualNum < expectNum;
                    return (isOk, $"数值比较: {actualNum} < {expectNum} ? {isOk}");
                }
                return (false, "LessThan需要数值类型");

            case "greaterorequal":
                if (isNumeric && expectIsNumeric)
                {
                    var isOk = actualNum >= expectNum;
                    return (isOk, $"数值比较: {actualNum} >= {expectNum} ? {isOk}");
                }
                return (false, "GreaterOrEqual需要数值类型");

            case "lessorequal":
                if (isNumeric && expectIsNumeric)
                {
                    var isOk = actualNum <= expectNum;
                    return (isOk, $"数值比较: {actualNum} <= {expectNum} ? {isOk}");
                }
                return (false, "LessOrEqual需要数值类型");

            case "range":
                if (isNumeric && minIsNumeric && maxIsNumeric)
                {
                    var isOk = actualNum >= expectMin && actualNum <= expectMax;
                    return (isOk, $"范围比较: {expectMin} <= {actualNum} <= {expectMax} ? {isOk}");
                }
                return (false, "Range需要数值类型");

            case "contains":
                var containsResult = actualStr.Contains(expectValue);
                return (containsResult, $"包含检查: '{actualStr}' 包含 '{expectValue}' ? {containsResult}");

            case "startswith":
                var startsWithResult = actualStr.StartsWith(expectValue);
                return (startsWithResult, $"前缀检查: '{actualStr}' 以 '{expectValue}' 开头 ? {startsWithResult}");

            case "endswith":
                var endsWithResult = actualStr.EndsWith(expectValue);
                return (endsWithResult, $"后缀检查: '{actualStr}' 以 '{expectValue}' 结尾 ? {endsWithResult}");

            default:
                return (false, $"未知的判定条件: {condition}");
        }
    }

    private OperatorExecutionOutput CreateNgOutput(string details, string ngOutputValue, object? actualValue = null)
    {
        var outputData = new Dictionary<string, object>
        {
            { "JudgmentResult", "NG" },
            { "JudgmentValue", ngOutputValue },
            { "ActualValue", actualValue ?? "null" },
            { "Details", details },
            { "IsOk", false }
        };

        if (int.TryParse(ngOutputValue, out var intValue))
        {
            outputData["JudgmentValueInt"] = intValue;
        }

        Logger.LogWarning("[ResultJudgment] 判定失败: {Details}", details);

        return OperatorExecutionOutput.Success(outputData);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var condition = GetStringParam(@operator, "Condition", "Equal");

        var validConditions = new[]
        {
            "Equal", "GreaterThan", "LessThan", "GreaterOrEqual", "LessOrEqual",
            "Range", "Contains", "StartsWith", "EndsWith"
        };

        if (!validConditions.Contains(condition, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"判定条件必须是以下之一: {string.Join(", ", validConditions)}");
        }

        var fieldName = GetStringParam(@operator, "FieldName", "");
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return ValidationResult.Invalid("判定字段不能为空");
        }

        return ValidationResult.Valid();
    }
}
