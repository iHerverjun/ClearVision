using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

public class DelayOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Delay;

    public DelayOperator(ILogger<DelayOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var ms = GetIntParam(@operator, "Milliseconds", 200, 0, 60000);
        var start = DateTime.UtcNow;
        await Task.Delay(ms, cancellationToken);
        var elapsed = (int)(DateTime.UtcNow - start).TotalMilliseconds;

        object? input = null;
        if (inputs != null && inputs.TryGetValue("Input", out var v)) input = v;

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Output"] = input ?? string.Empty,
            ["ElapsedMs"] = elapsed
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
