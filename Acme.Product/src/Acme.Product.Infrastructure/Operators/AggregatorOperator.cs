// AggregatorOperator.cs
// 聚合算子
// 对多路输入数据进行合并、汇总与统计处理
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
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
[OperatorParam("Mode", "聚合模式", "enum", DefaultValue = "Merge", Options = new[] { "Merge|合并列表", "Max|提取最大值", "Min|提取最小值", "Average|计算均值" })]
public class AggregatorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Aggregator;

    public AggregatorOperator(ILogger<AggregatorOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var mode = GetStringParam(@operator, "Mode", "Merge");
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

        var output = new Dictionary<string, object>
        {
            ["MergedList"] = list,
            ["MaxValue"] = max,
            ["MinValue"] = min,
            ["Average"] = avg
        };

        switch (mode.ToLowerInvariant())
        {
            case "max":
                output["Result"] = max;
                break;
            case "min":
                output["Result"] = min;
                break;
            case "average":
                output["Result"] = avg;
                break;
            default:
                output["Result"] = list;
                break;
        }

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
}
