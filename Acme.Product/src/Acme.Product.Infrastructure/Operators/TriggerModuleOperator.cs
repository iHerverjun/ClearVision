// TriggerModuleOperator.cs
// 触发模块算子
// 管理软件触发、定时触发与外部触发流程
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "触发模块",
    Description = "Generates software, timer, or external triggers.",
    Category = "逻辑工具",
    IconName = "trigger",
    Keywords = new[] { "trigger", "start", "timer", "external signal" }
)]
[InputPort("Signal", "Signal", PortDataType.Boolean, IsRequired = false)]
[OutputPort("Triggered", "Triggered", PortDataType.Boolean)]
[OutputPort("Timestamp", "Timestamp", PortDataType.String)]
[OutputPort("TriggerCount", "Trigger Count", PortDataType.Integer)]
[OperatorParam("TriggerMode", "Trigger Mode", "enum", DefaultValue = "Software", Options = new[] { "Software|Software", "Timer|Timer", "ExternalSignal|ExternalSignal" })]
[OperatorParam("Interval", "Interval (ms)", "int", DefaultValue = 1000, Min = 1, Max = 3600000)]
[OperatorParam("AutoRepeat", "Auto Repeat", "bool", DefaultValue = true)]
public class TriggerModuleOperator : OperatorBase
{
    private readonly object _syncRoot = new();
    private DateTime _lastTriggerUtc = DateTime.MinValue;
    private int _triggerCount;

    public override OperatorType OperatorType => OperatorType.TriggerModule;

    public TriggerModuleOperator(ILogger<TriggerModuleOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var mode = GetStringParam(@operator, "TriggerMode", "Software");
        var intervalMs = GetIntParam(@operator, "Interval", 1000, 1, 3_600_000);
        var autoRepeat = GetBoolParam(@operator, "AutoRepeat", true);

        var now = DateTime.UtcNow;
        var triggered = false;
        var count = 0;

        lock (_syncRoot)
        {
            if (mode.Equals("Software", StringComparison.OrdinalIgnoreCase))
            {
                triggered = true;
            }
            else if (mode.Equals("Timer", StringComparison.OrdinalIgnoreCase))
            {
                triggered = ShouldTriggerByTimer(now, intervalMs, autoRepeat);
            }
            else
            {
                triggered = TryGetSignalInput(inputs, out var signal) ? signal : true;
            }

            if (triggered)
            {
                _lastTriggerUtc = now;
                _triggerCount++;
            }

            count = _triggerCount;
        }

        var output = new Dictionary<string, object>
        {
            { "Triggered", triggered },
            { "Timestamp", now.ToString("O") },
            { "TriggerCount", count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "TriggerMode", "Software");
        var validModes = new[] { "Software", "Timer", "ExternalSignal" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("TriggerMode must be Software, Timer or ExternalSignal");
        }

        if (mode.Equals("Timer", StringComparison.OrdinalIgnoreCase))
        {
            var interval = GetIntParam(@operator, "Interval", 1000);
            if (interval <= 0)
            {
                return ValidationResult.Invalid("Interval must be greater than 0 in Timer mode");
            }
        }

        return ValidationResult.Valid();
    }

    private bool ShouldTriggerByTimer(DateTime now, int intervalMs, bool autoRepeat)
    {
        if (_triggerCount == 0)
        {
            return true;
        }

        if (!autoRepeat)
        {
            return false;
        }

        if (_lastTriggerUtc == DateTime.MinValue)
        {
            return true;
        }

        return (now - _lastTriggerUtc).TotalMilliseconds >= intervalMs;
    }

    private static bool TryGetSignalInput(Dictionary<string, object>? inputs, out bool signal)
    {
        signal = false;

        if (inputs == null)
        {
            return false;
        }

        if (inputs.TryGetValue("Signal", out var signalObj) && TryConvertToBool(signalObj, out signal))
        {
            return true;
        }

        return false;
    }

    private static bool TryConvertToBool(object? raw, out bool value)
    {
        value = false;

        if (raw is null)
        {
            return false;
        }

        return raw switch
        {
            bool b => (value = b) == b,
            int i => (value = i != 0) || i == 0,
            long l => (value = l != 0) || l == 0,
            double d => (value = Math.Abs(d) > double.Epsilon) || Math.Abs(d) <= double.Epsilon,
            _ => bool.TryParse(raw.ToString(), out value)
        };
    }
}

