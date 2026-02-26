// StatisticsOperator.cs
// 统计分析算子实现 - Sprint 3 Task 3.6e
// 支持均值/标准差/CPK/CP 工业级质量统计
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 统计分析算子 - CPK/均值统计
/// 
/// 功能：
/// - 累积历史数据计算均值、标准差、样本量
/// - 当提供 USL/LSL 时计算 Cp 和 Cpk（过程能力指数）
/// - Cpk ≥ 1.33 表示过程能力充足（工业标准）
/// 
/// 使用场景：
/// - 尺寸公差 SPC 监控
/// - 产线质量趋势分析
/// </summary>
[OperatorMeta(
    DisplayName = "统计分析",
    Description = "计算均值、标准差、CPK 等质量统计指标",
    Category = "通用",
    IconName = "stats"
)]
[InputPort("Value", "输入值", PortDataType.Float, IsRequired = true)]
[OutputPort("Mean", "均值", PortDataType.Float)]
[OutputPort("StdDev", "标准差", PortDataType.Float)]
[OutputPort("Count", "样本数", PortDataType.Integer)]
[OutputPort("Min", "最小值", PortDataType.Float)]
[OutputPort("Max", "最大值", PortDataType.Float)]
[OutputPort("Cpk", "过程能力指数", PortDataType.Float)]
[OutputPort("IsCapable", "能力达标", PortDataType.Boolean)]
[OperatorParam("USL", "规格上限", "double", Description = "Upper Specification Limit，留空则不计算 CPK", DefaultValue = "")]
[OperatorParam("LSL", "规格下限", "double", Description = "Lower Specification Limit，留空则不计算 CPK", DefaultValue = "")]
public class StatisticsOperator : OperatorBase
{
    private readonly List<double> _historyValues = new();

    public override OperatorType OperatorType => OperatorType.Statistics;

    public StatisticsOperator(ILogger<StatisticsOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputValue<double>(inputs, "Value", out var value))
            return Task.FromResult(OperatorExecutionOutput.Failure("输入值无效或不存在"));

        // 获取规格上下限参数（可选）
        double? usl = GetOptionalDoubleParam(@operator, "USL");
        double? lsl = GetOptionalDoubleParam(@operator, "LSL");

        lock (_historyValues)
        {
            _historyValues.Add(value);
            // 限制最大统计样本量
            if (_historyValues.Count > 1000)
                _historyValues.RemoveAt(0);

            int count = _historyValues.Count;
            double mean = _historyValues.Average();
            double sumOfSquares = _historyValues.Select(v => (v - mean) * (v - mean)).Sum();
            double stdDev = count > 1 ? Math.Sqrt(sumOfSquares / (count - 1)) : 0;

            var output = new Dictionary<string, object>
            {
                { "Mean", mean },
                { "StdDev", stdDev },
                { "Count", count },
                { "Min", _historyValues.Min() },
                { "Max", _historyValues.Max() },
                { "Range", _historyValues.Max() - _historyValues.Min() }
            };

            // CPK/CP 计算（需要 USL 和 LSL 且样本量 ≥ 2 且标准差 > 0）
            if (usl.HasValue && lsl.HasValue && count >= 2 && stdDev > 0)
            {
                double cp = (usl.Value - lsl.Value) / (6.0 * stdDev);
                double cpu = (usl.Value - mean) / (3.0 * stdDev);
                double cpl = (mean - lsl.Value) / (3.0 * stdDev);
                double cpk = Math.Min(cpu, cpl);

                output["Cp"] = Math.Round(cp, 4);
                output["Cpk"] = Math.Round(cpk, 4);
                output["CPU"] = Math.Round(cpu, 4);
                output["CPL"] = Math.Round(cpl, 4);
                output["USL"] = usl.Value;
                output["LSL"] = lsl.Value;
                output["IsCapable"] = cpk >= 1.33; // 工业标准：Cpk ≥ 1.33
            }

            Logger.LogDebug("[Statistics] Count={Count}, Mean={Mean:F4}, StdDev={StdDev:F4}", count, mean, stdDev);

            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
    }

    /// <summary>
    /// 获取可选的 double 参数
    /// </summary>
    private static double? GetOptionalDoubleParam(Operator @operator, string name)
    {
        var param = @operator.Parameters.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (param?.Value != null && double.TryParse(param.Value.ToString(), out var val))
            return val;
        return null;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        double? usl = GetOptionalDoubleParam(@operator, "USL");
        double? lsl = GetOptionalDoubleParam(@operator, "LSL");

        // 如果两者都提供，USL 必须大于 LSL
        if (usl.HasValue && lsl.HasValue && usl.Value <= lsl.Value)
        {
            return ValidationResult.Invalid("USL（规格上限）必须大于 LSL（规格下限）");
        }

        return ValidationResult.Valid();
    }
}
