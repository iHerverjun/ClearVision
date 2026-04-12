using System.Globalization;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "数据聚合",
    Description = "将多路输入数据合并为列表，并提取极值与均值",
    Category = "数据处理",
    IconName = "merge",
    Keywords = new[] { "聚合", "合并", "汇总", "最大值", "最小值", "均值", "多路合并", "Aggregate", "Merge", "Max", "Min", "Average" }
)]
[InputPort("Value1", "值 1", PortDataType.Any, IsRequired = false)]
[InputPort("Value2", "值 2", PortDataType.Any, IsRequired = false)]
[InputPort("Value3", "值 3", PortDataType.Any, IsRequired = false)]
[OutputPort("Result", "结果", PortDataType.Any)]
[OutputPort("MergedList", "合并列表", PortDataType.Any)]
[OutputPort("MaxValue", "最大值", PortDataType.Float)]
[OutputPort("MinValue", "最小值", PortDataType.Float)]
[OutputPort("Average", "均值", PortDataType.Float)]
[OutputPort("NumericCount", "数值数量", PortDataType.Integer)]
[OperatorParam("Mode", "聚合模式", "enum", DefaultValue = "Merge", Options = new[] { "Merge|合并列表", "Max|提取最大值", "Min|提取最小值", "Average|计算均值" })]
public class AggregatorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Aggregator;

    public AggregatorOperator(ILogger<AggregatorOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var mode = GetStringParam(@operator, "Mode", "Merge");
        var modeKey = mode.Trim().ToLowerInvariant();
        var validModes = new[] { "merge", "max", "min", "average" };
        if (!validModes.Contains(modeKey, StringComparer.Ordinal))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Mode must be Merge, Max, Min or Average."));
        }

        var mergedList = new List<object>();
        foreach (var key in new[] { "Value1", "Value2", "Value3" })
        {
            if (inputs != null && inputs.TryGetValue(key, out var value) && value != null)
            {
                mergedList.Add(value);
            }
        }

        var numericValues = new List<double>();
        foreach (var item in mergedList)
        {
            if (TryConvertToFiniteDouble(item, out var parsed))
            {
                numericValues.Add(parsed);
            }
        }

        var numericCount = numericValues.Count;
        var max = numericCount > 0 ? numericValues.Max() : 0.0;
        var min = numericCount > 0 ? numericValues.Min() : 0.0;
        var average = numericCount > 0 ? numericValues.Average() : 0.0;

        if (modeKey is "max" or "min" or "average")
        {
            if (numericCount == 0)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"Mode '{mode}' requires at least one finite numeric input."));
            }
        }

        var output = new Dictionary<string, object>
        {
            ["MergedList"] = mergedList,
            ["MaxValue"] = max,
            ["MinValue"] = min,
            ["Average"] = average,
            ["NumericCount"] = numericCount
        };

        output["Result"] = modeKey switch
        {
            "max" => max,
            "min" => min,
            "average" => average,
            _ => mergedList
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "Merge");
        var validModes = new[] { "Merge", "Max", "Min", "Average" };
        return validModes.Contains(mode, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.Valid()
            : ValidationResult.Invalid("Mode must be Merge, Max, Min or Average.");
    }

    private static bool TryConvertToFiniteDouble(object value, out double parsed)
    {
        parsed = 0;

        var success = value switch
        {
            double d => (parsed = d) == d || double.IsNaN(d),
            float f => (parsed = f) == f || float.IsNaN(f),
            byte b => (parsed = b) == b,
            sbyte sb => (parsed = sb) == sb,
            short s => (parsed = s) == s,
            ushort us => (parsed = us) == us,
            int i => (parsed = i) == i,
            uint ui => (parsed = ui) == ui,
            long l => (parsed = l) == l,
            ulong ul => (parsed = ul) == ul,
            decimal m => (parsed = (double)m) == (double)m,
            string text => double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed),
            IFormattable formattable => double.TryParse(formattable.ToString(null, CultureInfo.InvariantCulture), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed),
            _ => double.TryParse(value.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed)
        };

        return success && double.IsFinite(parsed);
    }
}
