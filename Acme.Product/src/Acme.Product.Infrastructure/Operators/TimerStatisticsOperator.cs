using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

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
