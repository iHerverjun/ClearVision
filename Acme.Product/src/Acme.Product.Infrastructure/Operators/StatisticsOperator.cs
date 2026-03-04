// StatisticsOperator.cs
// 统计算子
// 对输入数据执行统计聚合与指标输出
// 作者：蘅芜君
using System.Collections.Concurrent;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Statistics",
    Description = "Computes Mean/StdDev/Cpk statistics over rolling history.",
    Category = "General",
    IconName = "stats"
)]
[InputPort("Value", "Input Value", PortDataType.Float, IsRequired = true)]
[OutputPort("Mean", "Mean", PortDataType.Float)]
[OutputPort("StdDev", "StdDev", PortDataType.Float)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("Min", "Min", PortDataType.Float)]
[OutputPort("Max", "Max", PortDataType.Float)]
[OutputPort("Cpk", "Cpk", PortDataType.Float)]
[OutputPort("IsCapable", "Is Capable", PortDataType.Boolean)]
[OperatorParam("USL", "Upper Specification Limit", "double", DefaultValue = "", Description = "Optional. Cpk is calculated when both USL and LSL are provided.")]
[OperatorParam("LSL", "Lower Specification Limit", "double", DefaultValue = "", Description = "Optional. Cpk is calculated when both USL and LSL are provided.")]
[OperatorParam("WindowSize", "Window Size", "int", DefaultValue = 1000, Min = 2, Max = 50000)]
[OperatorParam("StateTtlMinutes", "State TTL Minutes", "int", DefaultValue = 120, Min = 1, Max = 10080)]
[OperatorParam("Reset", "Reset History", "bool", DefaultValue = false)]
public class StatisticsOperator : OperatorBase
{
    private static readonly ConcurrentDictionary<Guid, RollingHistoryState> HistoryByOperator = new();
    private static readonly object CleanupSync = new();
    private static DateTime _lastCleanupUtc = DateTime.MinValue;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

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
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input Value is required."));
        }

        var usl = GetOptionalDoubleParam(@operator, "USL");
        var lsl = GetOptionalDoubleParam(@operator, "LSL");
        var windowSize = GetIntParam(@operator, "WindowSize", 1000, min: 2, max: 50_000);
        var stateTtlMinutes = GetIntParam(@operator, "StateTtlMinutes", 120, min: 1, max: 10_080);
        var reset = GetBoolParam(@operator, "Reset", false);
        var nowUtc = DateTime.UtcNow;

        var state = HistoryByOperator.GetOrAdd(@operator.Id, _ => new RollingHistoryState());

        double[] snapshot;
        lock (state.SyncRoot)
        {
            if (reset)
            {
                state.Values.Clear();
            }

            state.Values.Enqueue(value);
            while (state.Values.Count > windowSize)
            {
                state.Values.Dequeue();
            }

            state.LastTouchedUtc = nowUtc;
            snapshot = state.Values.ToArray();
        }

        TryCleanupStaleStates(nowUtc, TimeSpan.FromMinutes(stateTtlMinutes));

        var count = snapshot.Length;
        var mean = snapshot.Average();
        var min = snapshot.Min();
        var max = snapshot.Max();
        var variance = count > 1
            ? snapshot.Select(v => (v - mean) * (v - mean)).Sum() / (count - 1)
            : 0.0;
        var stdDev = Math.Sqrt(Math.Max(0, variance));

        var output = new Dictionary<string, object>
        {
            { "Mean", mean },
            { "StdDev", stdDev },
            { "Count", count },
            { "Min", min },
            { "Max", max },
            { "Range", max - min },
            { "WindowSize", windowSize },
            { "StateTtlMinutes", stateTtlMinutes }
        };

        if (usl.HasValue && lsl.HasValue && count >= 2 && stdDev > 0)
        {
            var cp = (usl.Value - lsl.Value) / (6.0 * stdDev);
            var cpu = (usl.Value - mean) / (3.0 * stdDev);
            var cpl = (mean - lsl.Value) / (3.0 * stdDev);
            var cpk = Math.Min(cpu, cpl);

            output["Cp"] = Math.Round(cp, 4);
            output["Cpk"] = Math.Round(cpk, 4);
            output["CPU"] = Math.Round(cpu, 4);
            output["CPL"] = Math.Round(cpl, 4);
            output["USL"] = usl.Value;
            output["LSL"] = lsl.Value;
            output["IsCapable"] = cpk >= 1.33;
        }

        Logger.LogDebug(
            "[Statistics] Operator={OperatorId}, Count={Count}, Mean={Mean:F4}, StdDev={StdDev:F4}",
            @operator.Id,
            count,
            mean,
            stdDev);

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var usl = GetOptionalDoubleParam(@operator, "USL");
        var lsl = GetOptionalDoubleParam(@operator, "LSL");

        if (usl.HasValue && lsl.HasValue && usl.Value <= lsl.Value)
        {
            return ValidationResult.Invalid("USL must be greater than LSL.");
        }

        var windowSize = GetIntParam(@operator, "WindowSize", 1000);
        if (windowSize < 2 || windowSize > 50_000)
        {
            return ValidationResult.Invalid("WindowSize must be between 2 and 50000.");
        }

        var stateTtlMinutes = GetIntParam(@operator, "StateTtlMinutes", 120);
        if (stateTtlMinutes < 1 || stateTtlMinutes > 10_080)
        {
            return ValidationResult.Invalid("StateTtlMinutes must be between 1 and 10080.");
        }

        return ValidationResult.Valid();
    }

    private static void TryCleanupStaleStates(DateTime nowUtc, TimeSpan stateTtl)
    {
        if ((nowUtc - _lastCleanupUtc) < CleanupInterval)
        {
            return;
        }

        lock (CleanupSync)
        {
            if ((nowUtc - _lastCleanupUtc) < CleanupInterval)
            {
                return;
            }

            var staleBefore = nowUtc - stateTtl;
            foreach (var entry in HistoryByOperator)
            {
                var shouldRemove = false;
                var state = entry.Value;
                lock (state.SyncRoot)
                {
                    shouldRemove = state.LastTouchedUtc < staleBefore;
                }

                if (shouldRemove)
                {
                    HistoryByOperator.TryRemove(entry.Key, out _);
                }
            }

            _lastCleanupUtc = nowUtc;
        }
    }

    private static double? GetOptionalDoubleParam(Operator @operator, string name)
    {
        var param = @operator.Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (param?.Value == null)
        {
            return null;
        }

        return double.TryParse(param.Value.ToString(), out var value) ? value : null;
    }

    private sealed class RollingHistoryState
    {
        public object SyncRoot { get; } = new();

        public Queue<double> Values { get; } = new();

        public DateTime LastTouchedUtc { get; set; } = DateTime.UtcNow;
    }
}
