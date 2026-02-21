using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

public class AggregatorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Aggregator;

    public AggregatorOperator(ILogger<AggregatorOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var list = new List<object>();
        foreach (var key in new[] { "Value1", "Value2", "Value3" })
        {
            if (inputs != null && inputs.TryGetValue(key, out var value) && value != null)
            {
                list.Add(value);
            }
        }

        var numeric = list.Select(v => double.TryParse(v.ToString(), out var n) ? n : (double?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        var max = numeric.Count > 0 ? numeric.Max() : 0;
        var min = numeric.Count > 0 ? numeric.Min() : 0;
        var avg = numeric.Count > 0 ? numeric.Average() : 0;

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["MergedList"] = list,
            ["MaxValue"] = max,
            ["MinValue"] = min,
            ["Average"] = avg
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
