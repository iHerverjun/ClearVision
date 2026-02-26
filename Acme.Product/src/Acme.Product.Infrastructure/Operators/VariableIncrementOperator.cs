// VariableIncrementOperator.cs
// 变量递增算子 - 计数器自增// 功能实现自减
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 变量递增算子 - 计数器自增/自减
/// 【第三优先级】变量表/全局上下文功能
/// </summary>
[OperatorMeta(
    DisplayName = "变量递增",
    Description = "计数器自增/自减，支持重置条件",
    Category = "变量",
    IconName = "counter"
)]
[OutputPort("VariableName", "变量名", PortDataType.String)]
[OutputPort("PreviousValue", "前值", PortDataType.Integer)]
[OutputPort("NewValue", "新值", PortDataType.Integer)]
[OutputPort("Delta", "增量", PortDataType.Integer)]
[OutputPort("WasReset", "是否已重置", PortDataType.Boolean)]
[OperatorParam("VariableName", "变量名", "string", Description = "计数器变量名称", DefaultValue = "counter")]
[OperatorParam("Delta", "增量", "int", Description = "每次递增的值（可为负数实现递减）", DefaultValue = 1)]
[OperatorParam("ResetCondition", "重置条件", "enum", Description = "满足条件时重置计数器", DefaultValue = "None", Options = new[] { "None|不重置", "GreaterThan|大于阈值", "LessThan|小于阈值", "Equal|等于阈值" })]
[OperatorParam("ResetThreshold", "重置阈值", "int", DefaultValue = 100)]
[OperatorParam("ResetValue", "重置后值", "int", Description = "重置后的起始值", DefaultValue = 0)]
public class VariableIncrementOperator : OperatorBase
{
    private readonly IVariableContext _variableContext;
    
    public override OperatorType OperatorType => OperatorType.VariableIncrement;

    public VariableIncrementOperator(
        ILogger<VariableIncrementOperator> logger,
        IVariableContext variableContext) : base(logger)
    {
        _variableContext = variableContext;
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var variableName = GetStringParam(@operator, "VariableName", "");
        var delta = GetIntParam(@operator, "Delta", 1); // 可以为负数实现递减
        var resetCondition = GetStringParam(@operator, "ResetCondition", "None"); // None / GreaterThan / LessThan
        var resetThreshold = GetIntParam(@operator, "ResetThreshold", 0);
        var resetValue = GetIntParam(@operator, "ResetValue", 0);

        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("变量名不能为空"));
        }

        // 先获取当前值
        var currentValue = _variableContext.GetValue<long>(variableName, 0L);
        
        // 检查重置条件
        bool shouldReset = false;
        switch (resetCondition.ToLower())
        {
            case "greaterthan":
                shouldReset = currentValue > resetThreshold;
                break;
            case "lessthan":
                shouldReset = currentValue < resetThreshold;
                break;
            case "equal":
                shouldReset = currentValue == resetThreshold;
                break;
        }

        long newValue;
        if (shouldReset)
        {
            newValue = resetValue + delta;
            Logger.LogInformation("[VariableIncrement] 变量 {VariableName} 重置为 {ResetValue} 后递增 {Delta}", 
                variableName, resetValue, delta);
        }
        else
        {
            newValue = _variableContext.Increment(variableName, delta);
        }

        Logger.LogInformation("[VariableIncrement] 变量 {VariableName}: {CurrentValue} + {Delta} = {NewValue}", 
            variableName, currentValue, delta, newValue);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "VariableName", variableName },
            { "PreviousValue", currentValue },
            { "NewValue", newValue },
            { "Delta", delta },
            { "WasReset", shouldReset },
            { "CycleCount", _variableContext.CycleCount }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var variableName = GetStringParam(@operator, "VariableName", "");
        if (string.IsNullOrWhiteSpace(variableName))
            return ValidationResult.Invalid("变量名不能为空");

        var validConditions = new[] { "none", "greaterthan", "lessthan", "equal" };
        var condition = GetStringParam(@operator, "ResetCondition", "None").ToLower();
        if (!validConditions.Contains(condition))
            return ValidationResult.Invalid($"不支持的重置条件: {condition}");

        return ValidationResult.Valid();
    }
}
