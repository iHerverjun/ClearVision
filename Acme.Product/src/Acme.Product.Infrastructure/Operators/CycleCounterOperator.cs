// CycleCounterOperator.cs
// 循环计数器算子 - 获取当前循环次数和统计信息
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 循环计数器算子 - 获取当前循环次数和统计信息
/// 【第三优先级】循环计数器功能
/// </summary>
public class CycleCounterOperator : OperatorBase
{
    private readonly IVariableContext _variableContext;
    
    public override OperatorType OperatorType => OperatorType.CycleCounter;

    public CycleCounterOperator(
        ILogger<CycleCounterOperator> logger,
        IVariableContext variableContext) : base(logger)
    {
        _variableContext = variableContext;
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var action = GetStringParam(@operator, "Action", "Read"); // Read / Reset / Increment
        var maxCycles = GetIntParam(@operator, "MaxCycles", 0); // 0表示无限制

        long currentCount = _variableContext.CycleCount;
        bool isLimitReached = maxCycles > 0 && currentCount >= maxCycles;

        switch (action.ToLower())
        {
            case "reset":
                _variableContext.ResetCycleCount();
                currentCount = 0;
                Logger.LogInformation("[CycleCounter] 循环计数器已重置");
                break;
                
            case "increment":
                _variableContext.IncrementCycleCount();
                currentCount = _variableContext.CycleCount;
                isLimitReached = maxCycles > 0 && currentCount >= maxCycles;
                Logger.LogInformation("[CycleCounter] 循环计数器递增: {Count}", currentCount);
                break;
                
            default: // Read
                Logger.LogDebug("[CycleCounter] 读取循环计数: {Count}", currentCount);
                break;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "CycleCount", currentCount },
            { "MaxCycles", maxCycles },
            { "IsLimitReached", isLimitReached },
            { "RemainingCycles", maxCycles > 0 ? Math.Max(0, maxCycles - currentCount) : -1 },
            { "Progress", maxCycles > 0 ? (double)currentCount / maxCycles * 100 : 0 }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var validActions = new[] { "read", "reset", "increment" };
        var action = GetStringParam(@operator, "Action", "Read").ToLower();
        if (!validActions.Contains(action))
            return ValidationResult.Invalid($"不支持的操作: {action}");

        return ValidationResult.Valid();
    }
}
