using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

public class ComparatorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Comparator;

    public ComparatorOperator(ILogger<ComparatorOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var a = TryRead(inputs, "ValueA");
        var hasB = TryRead(inputs, "ValueB", out var b);
        if (!hasB) b = GetDoubleParam(@operator, "CompareValue", 0);

        var condition = GetStringParam(@operator, "Condition", "GreaterThan");
        var tolerance = Math.Abs(GetDoubleParam(@operator, "Tolerance", 0.0001));
        var min = GetDoubleParam(@operator, "RangeMin", 0);
        var max = GetDoubleParam(@operator, "RangeMax", 1);
        var diff = a - b;

        var result = condition.ToLower() switch
        {
            "greaterthan" => a > b,
            "greaterthanorequal" => a >= b,
            "lessthan" => a < b,
            "lessthanorequal" => a <= b,
            "equal" => Math.Abs(diff) <= tolerance,
            "notequal" => Math.Abs(diff) > tolerance,
            "inrange" => a >= min && a <= max,
            _ => false
        };

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Result"] = result,
            ["Difference"] = diff
        }));
    }

    private static double TryRead(Dictionary<string, object>? inputs, string key)
        => TryRead(inputs, key, out var value) ? value : 0d;

    private static bool TryRead(Dictionary<string, object>? inputs, string key, out double value)
    {
        value = 0;
        if (inputs == null || !inputs.TryGetValue(key, out var obj) || obj == null) return false;
        return double.TryParse(obj.ToString(), out value);
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
