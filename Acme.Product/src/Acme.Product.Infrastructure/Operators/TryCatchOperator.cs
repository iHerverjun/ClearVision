// TryCatchOperator.cs
// 异常处理算子 - Try-Catch 流程控制
// 作者：蘅芜君
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 异常处理算子 - Try-Catch 流程控制。
/// 算子本身不执行异常捕获，仅输出统一的流程控制契约。
/// </summary>
[OperatorMeta(
    DisplayName = "异常捕获",
    Description = "Try-Catch 流程控制",
    Category = "流程控制",
    IconName = "trycatch"
)]
[InputPort("Input", "输入", PortDataType.Any, IsRequired = false)]
[OutputPort("Try", "Try分支", PortDataType.Any)]
[OutputPort("Catch", "Catch分支", PortDataType.Any)]
[OutputPort("Error", "错误信息", PortDataType.String)]
[OutputPort("HasError", "是否有错", PortDataType.Boolean)]
[OperatorParam("EnableCatch", "启用Catch", "bool", Description = "是否启用异常捕获", DefaultValue = true)]
[OperatorParam("CatchOutputError", "输出错误信息", "bool", DefaultValue = true)]
[OperatorParam("CatchOutputStackTrace", "输出堆栈", "bool", DefaultValue = false)]
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
        var enableCatch = GetBoolParam(@operator, "EnableCatch", true);
        _ = GetBoolParam(@operator, "CatchOutputError", true);
        _ = GetBoolParam(@operator, "CatchOutputStackTrace", false);

        object? input = null;
        inputs?.TryGetValue("Input", out input);

        var outputData = new Dictionary<string, object>
        {
            ["Try"] = input != null ? PreserveOutputValue(input) : null!,
            ["Catch"] = null!,
            ["Error"] = string.Empty,
            ["HasError"] = false
        };

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
        return ValidationResult.Valid();
    }
}
