using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

public class CommentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Comment;

    public CommentOperator(ILogger<CommentOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        object? input = null;
        if (inputs != null && inputs.TryGetValue("Input", out var value))
        {
            input = value;
        }

        var text = GetStringParam(@operator, "Text", string.Empty);
        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Output"] = input ?? string.Empty,
            ["Message"] = text
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
