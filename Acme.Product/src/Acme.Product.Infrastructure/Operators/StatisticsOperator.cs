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
[OperatorParam("Reset", "Reset History", "bool", DefaultValue = false)]
public class StatisticsOperator : OperatorBase
{
    private static readonly ConcurrentDictionary<Guid, RollingHistoryState> HistoryByOperator = new();

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
        var reset = GetBoolParam(@operator, "Reset", false);

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

            snapshot = state.Values.ToArray();
        }

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
            { "WindowSize", windowSize }
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

        return ValidationResult.Valid();
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
    }
}
