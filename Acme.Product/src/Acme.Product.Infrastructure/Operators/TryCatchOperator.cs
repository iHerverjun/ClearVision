// TryCatchOperator.cs
// 异常处理算子 - Try-Catch 流程控制
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 异常处理算子 - Try-Catch 流程控制
/// 【第三优先级】异常处理算子
/// </summary>
/// <remarks>
/// 此算子作为流程控制节点，本身不执行具体逻辑，
/// 而是通过输出端口路由到 Try 分支或 Catch 分支。
/// 实际的异常捕获由 FlowExecutionService 在流程执行时处理。
/// </remarks>
public class TryCatchOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TryCatch;

    public TryCatchOperator(ILogger<TryCatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // TryCatch 算子本身只是流程控制节点
        // 实际的 try-catch 逻辑在 FlowExecutionService 中实现
        // 这里只是透传输入数据到 Try 输出端口
        
        var enableCatch = GetBoolParam(@operator, "EnableCatch", true);
        var catchOutputError = GetBoolParam(@operator, "CatchOutputError", true);
        var catchOutputStackTrace = GetBoolParam(@operator, "CatchOutputStackTrace", false);

        var outputData = new Dictionary<string, object>();

        // 将输入数据透传到 Try 端口
        if (inputs != null)
        {
            foreach (var kvp in inputs)
            {
                outputData[kvp.Key] = PreserveOutputValue(kvp.Value);
            }
        }

        outputData["TryCatch_Enabled"] = enableCatch;
        outputData["TryCatch_OutputError"] = catchOutputError;
        outputData["TryCatch_OutputStackTrace"] = catchOutputStackTrace;

        Logger.LogDebug("[TryCatch] 异常处理节点已激活，Catch启用: {EnableCatch}", enableCatch);

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private static object PreserveOutputValue(object value)
    {
        if (value is ImageWrapper wrapper)
            return wrapper.AddRef();
        return value;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        // TryCatch 算子参数验证较宽松
        return ValidationResult.Valid();
    }
}
