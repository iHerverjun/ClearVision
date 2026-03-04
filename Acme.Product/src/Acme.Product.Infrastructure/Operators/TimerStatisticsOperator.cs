// TimerStatisticsOperator.cs
// 计时统计算子
// 统计流程或算子的耗时并输出指标
// 作者：蘅芜君
using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "计时统计",
    Description = "Measures elapsed and cycle time statistics.",
    Category = "逻辑工具",
    IconName = "timer",
    Keywords = new[] { "timer", "elapsed", "cycle time", "ct", "statistics" }
)]
[InputPort("Trigger", "Trigger", PortDataType.Any, IsRequired = false)]
[OutputPort("ElapsedMs", "Elapsed (ms)", PortDataType.Float)]
[OutputPort("TotalMs", "Total (ms)", PortDataType.Float)]
[OutputPort("AverageMs", "Average (ms)", PortDataType.Float)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "SingleShot", Options = new[] { "SingleShot|SingleShot", "Cumulative|Cumulative" })]
[OperatorParam("ResetInterval", "Reset Interval", "int", DefaultValue = 0, Min = 0, Max = 1000000)]
public class TimerStatisticsOperator : OperatorBase
{
    private readonly object _syncRoot = new();
    private readonly Stopwatch _intervalStopwatch = new();
    private bool _started;
    private double _totalMs;
    private int _count;

    public override OperatorType OperatorType => OperatorType.TimerStatistics;

    public TimerStatisticsOperator(ILogger<TimerStatisticsOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var mode = GetStringParam(@operator, "Mode", "SingleShot");
        var resetInterval = GetIntParam(@operator, "ResetInterval", 0, 0, 1_000_000);

        double elapsedMs;
        double totalMs;
        double averageMs;
        int count;

        lock (_syncRoot)
        {
            if (!_started)
            {
                _intervalStopwatch.Start();
                _started = true;
                elapsedMs = 0;
            }
            else
            {
                elapsedMs = _intervalStopwatch.Elapsed.TotalMilliseconds;
                _intervalStopwatch.Restart();
            }

            if (mode.Equals("Cumulative", StringComparison.OrdinalIgnoreCase))
            {
                _count++;
                _totalMs += elapsedMs;

                totalMs = _totalMs;
                count = _count;
                averageMs = _count > 0 ? _totalMs / _count : 0;

                if (resetInterval > 0 && _count >= resetInterval)
                {
                    _count = 0;
                    _totalMs = 0;
                    _intervalStopwatch.Restart();
                }
            }
            else
            {
                totalMs = elapsedMs;
                averageMs = elapsedMs;
                count = 1;
            }
        }

        var output = new Dictionary<string, object>
        {
            { "ElapsedMs", elapsedMs },
            { "TotalMs", totalMs },
            { "AverageMs", averageMs },
            { "Count", count }
        };

        if (inputs != null && inputs.TryGetValue("Trigger", out var trigger))
        {
            output["Trigger"] = trigger;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "SingleShot");
        var validModes = new[] { "SingleShot", "Cumulative" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be SingleShot or Cumulative");
        }

        var resetInterval = GetIntParam(@operator, "ResetInterval", 0);
        if (resetInterval < 0)
        {
            return ValidationResult.Invalid("ResetInterval must be >= 0");
        }

        return ValidationResult.Valid();
    }
}

