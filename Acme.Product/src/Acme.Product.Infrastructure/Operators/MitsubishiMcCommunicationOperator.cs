using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Acme.PlcComm.Mitsubishi;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Mitsubishi MC Communication",
    Description = "Mitsubishi MC protocol PLC read/write communication.",
    Category = "Communication",
    IconName = "mc-plc",
    Keywords = new[] { "PLC", "Mitsubishi", "MC", "Read", "Write" }
)]
[InputPort("Data", "Data", PortDataType.Any, IsRequired = false)]
[OutputPort("Response", "Response", PortDataType.String)]
[OutputPort("Status", "Status", PortDataType.Boolean)]
[OperatorParam("IpAddress", "IP Address", "string", DefaultValue = "192.168.3.39")]
[OperatorParam("Port", "Port", "int", DefaultValue = 5002, Min = 1, Max = 65535)]
[OperatorParam("UseGlobalFallback", "Use Global Fallback", "bool", DefaultValue = false)]
[OperatorParam("Address", "PLC Address", "string", DefaultValue = "D100")]
[OperatorParam("Length", "Read Length", "int", DefaultValue = 1, Min = 1, Max = 999)]
[OperatorParam("DataType", "Data Type", "enum", DefaultValue = "Word", Options = new[]
{
    "Bit|Bit(Bool)",
    "Word|Word(UInt16)",
    "Int16|Int16",
    "DWord|DWord(UInt32)",
    "Int32|Int32",
    "Float|Float",
    "Double|Double"
})]
[OperatorParam("Operation", "Operation", "enum", DefaultValue = "Read", Options = new[] { "Read|Read", "Write|Write" })]
[OperatorParam("WriteValue", "Write Value", "string", DefaultValue = "")]
[OperatorParam("PollingMode", "Polling Mode", "enum", Description = "Whether to poll while reading.", DefaultValue = "None", Options = new[] { "None|None", "WaitForValue|Wait For Value" })]
[OperatorParam("PollingCondition", "Polling Condition", "enum", Description = "Condition for polling.", DefaultValue = "Equal", Options = new[] { "Equal|Equal", "NotEqual|Not Equal", "GreaterThan|Greater Than", "LessThan|Less Than", "GreaterOrEqual|Greater Or Equal", "LessOrEqual|Less Or Equal" })]
[OperatorParam("PollingValue", "Polling Value", "string", Description = "Target value for polling.", DefaultValue = "1")]
[OperatorParam("PollingTimeout", "Polling Timeout (ms)", "int", Description = "Maximum wait duration in milliseconds.", DefaultValue = 30000, Min = 100, Max = 300000)]
[OperatorParam("PollingInterval", "Polling Interval (ms)", "int", Description = "Interval between polling reads in milliseconds.", DefaultValue = 50, Min = 10, Max = 5000)]
public sealed class MitsubishiMcCommunicationOperator : PlcCommunicationOperatorBase
{
    public override OperatorType OperatorType => OperatorType.MitsubishiMcCommunication;

    public MitsubishiMcCommunicationOperator(ILogger<MitsubishiMcCommunicationOperator> logger) : base(logger)
    {
    }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var operatorIpAddress = GetStringParam(@operator, "IpAddress", string.Empty);
        var operatorPort = GetIntParam(@operator, "Port", 0);
        var useGlobalFallback = GetBoolParam(@operator, "UseGlobalFallback", false);
        var address = GetStringParam(@operator, "Address", "D100");
        var length = GetIntParam(@operator, "Length", 1, 1, 999);
        var dataType = GetStringParam(@operator, "DataType", "Word");
        var operation = GetStringParam(@operator, "Operation", "Read");
        var pollingMode = GetStringParam(@operator, "PollingMode", "None");
        var pollingCondition = GetStringParam(@operator, "PollingCondition", "Equal");
        var pollingValue = GetStringParam(@operator, "PollingValue", "1");
        var pollingTimeout = GetIntParam(@operator, "PollingTimeout", 30000, 100, 300000);
        var pollingInterval = GetIntParam(@operator, "PollingInterval", 50, 10, 5000);
        var writeValue = ResolveWriteValue(@operator, inputs);

        var logIp = string.IsNullOrWhiteSpace(operatorIpAddress) ? "(unset)" : operatorIpAddress;
        var logPort = operatorPort;

        try
        {
            var (ipAddress, port, _, connectionSource) = ResolveConnectionSettings(
                operatorIpAddress,
                operatorPort,
                "MC",
                useGlobalFallback);
            logIp = ipAddress;
            logPort = port;

            var connectionKey = $"MC:{ipAddress}:{port}";
            var (client, _) = await GetOrCreateConnectionAsync(connectionKey, () =>
            {
                var mcClient = PlcClientFactory.CreateMitsubishiMc(ipAddress);
                if (mcClient is MitsubishiMcClient typed)
                {
                    typed.Port = port;
                }

                return mcClient;
            });

            if (operation.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                if (pollingMode.Equals("WaitForValue", StringComparison.OrdinalIgnoreCase))
                {
                    var pollingReadOutput = await ExecuteReadWithPollingAsync(
                        client,
                        address,
                        dataType,
                        (ushort)length,
                        pollingCondition,
                        pollingValue,
                        pollingTimeout,
                        pollingInterval,
                        cancellationToken);
                    AttachConnectionAuditInfo(pollingReadOutput, connectionSource);
                    return pollingReadOutput;
                }

                var readOutput = await ExecuteReadAsync(client, address, dataType, (ushort)length, cancellationToken);
                AttachConnectionAuditInfo(readOutput, connectionSource);
                return readOutput;
            }

            var writeOutput = await ExecuteWriteAsync(client, address, dataType, writeValue, cancellationToken);
            AttachConnectionAuditInfo(writeOutput, connectionSource);
            return writeOutput;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MitsubishiMC] Communication error: {IP}:{Port} - {Message}", logIp, logPort, ex.Message);
            return CreateFailureOutput($"MC communication error: {ex.Message}");
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteReadAsync(
        IPlcClient client,
        string address,
        string dataType,
        ushort length,
        CancellationToken ct)
    {
        var result = await client.ReadAsync(address, length, ct);
        if (!result.IsSuccess)
        {
            return CreateFailureOutput($"Read failed: {result.Message}");
        }

        var value = ConvertBytesToValue(client, result.Content!, dataType);
        return CreateSuccessOutput(value, dataType);
    }

    private async Task<OperatorExecutionOutput> ExecuteWriteAsync(
        IPlcClient client,
        string address,
        string dataType,
        string writeValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(writeValue))
        {
            return CreateFailureOutput("WriteValue cannot be empty.");
        }

        var bytes = ConvertValueToBytes(client, writeValue, dataType);
        var result = await client.WriteAsync(address, bytes, ct);
        if (!result.IsSuccess)
        {
            return CreateFailureOutput($"Write failed: {result.Message}");
        }

        return CreateSuccessOutput(writeValue, dataType);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operatorIpAddress = GetStringParam(@operator, "IpAddress", string.Empty);
        var operatorPort = GetIntParam(@operator, "Port", 0);
        var useGlobalFallback = GetBoolParam(@operator, "UseGlobalFallback", false);
        var address = GetStringParam(@operator, "Address", string.Empty);
        var length = GetIntParam(@operator, "Length", 1);
        var pollingMode = GetStringParam(@operator, "PollingMode", "None");

        try
        {
            ResolveConnectionSettings(operatorIpAddress, operatorPort, "MC", useGlobalFallback);
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ex.Message);
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return ValidationResult.Invalid("Address cannot be empty.");
        }

        if (length < 1 || length > 999)
        {
            return ValidationResult.Invalid("Length must be within [1, 999].");
        }

        var validPollingModes = new[] { "None", "WaitForValue" };
        if (!validPollingModes.Contains(pollingMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"PollingMode must be one of: {string.Join(", ", validPollingModes)}.");
        }

        return ValidationResult.Valid();
    }

    private async Task<OperatorExecutionOutput> ExecuteReadWithPollingAsync(
        IPlcClient client,
        string address,
        string dataType,
        ushort length,
        string pollingCondition,
        string pollingValue,
        int timeoutMs,
        int intervalMs,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var readCount = 0;

        while (true)
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (elapsedMs > timeoutMs)
            {
                return CreateFailureOutput($"Polling timeout: waiting for {pollingCondition} {pollingValue} exceeded {timeoutMs}ms.");
            }

            ct.ThrowIfCancellationRequested();

            var result = await client.ReadAsync(address, length, ct);
            if (!result.IsSuccess)
            {
                await Task.Delay(Math.Min(intervalMs, 1000), ct);
                continue;
            }

            var currentValue = ConvertBytesToValue(client, result.Content!, dataType);
            readCount++;

            if (EvaluatePollingCondition(currentValue, pollingCondition, pollingValue))
            {
                var output = CreateSuccessOutput(currentValue, dataType);
                output.OutputData ??= new Dictionary<string, object>();
                output.OutputData["PollingReadCount"] = readCount;
                output.OutputData["PollingElapsedMs"] = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                output.OutputData["PollingMatched"] = true;
                return output;
            }

            await Task.Delay(intervalMs, ct);
        }
    }

    private static bool EvaluatePollingCondition(object currentValue, string condition, string targetValue)
    {
        var currentStr = currentValue?.ToString() ?? string.Empty;
        var currentIsNumeric = double.TryParse(currentStr, out var currentNum);
        var targetIsNumeric = double.TryParse(targetValue, out var targetNum);

        return condition.ToLowerInvariant() switch
        {
            "equal" => currentIsNumeric && targetIsNumeric
                ? Math.Abs(currentNum - targetNum) < 0.0001
                : currentStr.Equals(targetValue, StringComparison.OrdinalIgnoreCase),
            "notequal" => currentIsNumeric && targetIsNumeric
                ? Math.Abs(currentNum - targetNum) >= 0.0001
                : !currentStr.Equals(targetValue, StringComparison.OrdinalIgnoreCase),
            "greaterthan" => currentIsNumeric && targetIsNumeric && currentNum > targetNum,
            "lessthan" => currentIsNumeric && targetIsNumeric && currentNum < targetNum,
            "greaterorequal" => currentIsNumeric && targetIsNumeric && currentNum >= targetNum,
            "lessorequal" => currentIsNumeric && targetIsNumeric && currentNum <= targetNum,
            _ => false
        };
    }

    private static string ResolveWriteValue(Operator @operator, Dictionary<string, object>? inputs)
    {
        var staticValue = @operator.Parameters.FirstOrDefault(p => p.Name.Equals("WriteValue", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? string.Empty;
        if (inputs == null || inputs.Count == 0)
        {
            return staticValue;
        }

        foreach (var key in new[] { "JudgmentValue", "Value", "Data" })
        {
            if (!inputs.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            var parsed = value.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }
        }

        return staticValue;
    }
}
