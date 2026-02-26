using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "注释",
    Description = "在工作流中添加说明文本，不影响数据流，仅用于标注设计意图",
    Category = "辅助",
    IconName = "comment",
    Keywords = new[] { "注释", "备注", "说明", "标注", "文本", "Comment", "Note", "Annotation" }
)]
[InputPort("Input", "透传输入", PortDataType.Any, IsRequired = false)]
[OutputPort("Output", "透传输出", PortDataType.Any)]
[OutputPort("Message", "注释内容", PortDataType.String)]
[OperatorParam("Text", "注释文本", "string", DefaultValue = "")]
public class CommentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Comment;

    public CommentOperator(ILogger<CommentOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        object? input = null;
        if (inputs != null && inputs.TryGetValue("Input", out var value))
        {
            input = PreserveOutputValue(value);
        }

        var text = GetStringParam(@operator, "Text", string.Empty);
        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Output"] = input ?? string.Empty,
            ["Message"] = text
        }));
    }

    private static object PreserveOutputValue(object value)
    {
        if (value is ImageWrapper wrapper)
            return wrapper.AddRef();
        return value;
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
