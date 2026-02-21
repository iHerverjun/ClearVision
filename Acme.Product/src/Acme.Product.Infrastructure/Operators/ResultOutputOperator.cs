// ResultOutputOperator.cs
// 结果输出算子执行器
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 结果输出算子执行器
/// 透传输入数据作为输出，用于流程最终结果输出
/// </summary>
public class ResultOutputOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ResultOutput;

    public ResultOutputOperator(ILogger<ResultOutputOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 直接传递输入作为输出（透传模式）
        var output = new Dictionary<string, object>();

        if (inputs?.TryGetValue("Image", out var image) == true)
            output["Image"] = PreserveOutputValue(image);

        if (inputs?.TryGetValue("Result", out var result) == true)
            output["Result"] = result;

        // 透传其他可能的数据
        if (inputs != null)
        {
            foreach (var kvp in inputs)
            {
                if (!output.ContainsKey(kvp.Key))
                {
                    output[kvp.Key] = PreserveOutputValue(kvp.Value);
                }
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    private static object PreserveOutputValue(object value)
    {
        // 透传 ImageWrapper 时必须 AddRef，否则 ExecuteWithLifecycle finally 会 Release 输入并导致输出悬空
        if (value is ImageWrapper wrapper)
            return wrapper.AddRef();
        return value;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        // 结果输出算子不需要特定参数验证
        return ValidationResult.Valid();
    }
}
