// MitsubishiMcCommunicationOperator.cs
// 解析写入值：优先从上游输入获取，否则使用参数面板静态值
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 三菱MC通信算子
/// 支持3E帧二进制模式，适用于FX5U/Q/iQ-R/iQ-F系列PLC
/// </summary>
public class MitsubishiMcCommunicationOperator : PlcCommunicationOperatorBase
{
    public override OperatorType OperatorType => OperatorType.MitsubishiMcCommunication;

    public MitsubishiMcCommunicationOperator(ILogger<MitsubishiMcCommunicationOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var ipAddress = GetStringParam(@operator, "IpAddress", "192.168.3.1");
        var port = GetIntParam(@operator, "Port", 5002, 1, 65535);
        var address = GetStringParam(@operator, "Address", "D100");
        var length = GetIntParam(@operator, "Length", 1, 1, 960);
        var dataType = GetStringParam(@operator, "DataType", "Word");
        var operation = GetStringParam(@operator, "Operation", "Read");

        // 【第二优先级】轮询等待模式参数
        var pollingMode = GetStringParam(@operator, "PollingMode", "None"); // None / WaitForValue
        var pollingCondition = GetStringParam(@operator, "PollingCondition", "Equal"); // Equal / NotEqual / GreaterThan / LessThan
        var pollingValue = GetStringParam(@operator, "PollingValue", "1");
        var pollingTimeout = GetIntParam(@operator, "PollingTimeout", 30000, 100, 300000); // 100ms - 5min
        var pollingInterval = GetIntParam(@operator, "PollingInterval", 50, 10, 5000); // 10ms - 5s

        // 【增强】支持从上游输入动态获取写入值
        var writeValue = ResolveWriteValue(@operator, inputs);

        // 构建连接键
        var connectionKey = $"MC:{ipAddress}:{port}";

        try
        {
            // 获取或创建连接
            var (client, isNew) = await GetOrCreateConnectionAsync(connectionKey, () =>
            {
                var mcClient = PlcClientFactory.CreateMitsubishiMc(ipAddress);
                ((Acme.PlcComm.Mitsubishi.MitsubishiMcClient)mcClient).Port = port;
                return mcClient;
            });

            if (operation.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                // 【第二优先级】支持轮询等待模式
                if (pollingMode.Equals("WaitForValue", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExecuteReadWithPollingAsync(client, address, dataType, (ushort)length,
                        pollingCondition, pollingValue, pollingTimeout, pollingInterval, cancellationToken);
                }
                return await ExecuteReadAsync(client, address, dataType, (ushort)length, cancellationToken);
            }
            else
            {
                return await ExecuteWriteAsync(client, address, dataType, writeValue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MitsubishiMC] 通信错误: {IP}:{Port} - {Message}", ipAddress, port, ex.Message);
            return CreateFailureOutput($"MC通信错误: {ex.Message}");
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteReadAsync(
        IPlcClient client, string address, string dataType, ushort length, CancellationToken ct)
    {
        var result = await client.ReadAsync(address, length, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"读取失败: {result.Message}");

        var value = ConvertBytesToValue(result.Content!, dataType);
        Logger.LogInformation("[MitsubishiMC] 读取成功: {Address} = {Value}", address, value);
        return CreateSuccessOutput(value, dataType);
    }

    private async Task<OperatorExecutionOutput> ExecuteWriteAsync(
        IPlcClient client, string address, string dataType, string writeValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(writeValue))
            return CreateFailureOutput("写入值不能为空");

        var bytes = ConvertValueToBytes(writeValue, dataType);
        var result = await client.WriteAsync(address, bytes, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"写入失败: {result.Message}");

        Logger.LogInformation("[MitsubishiMC] 写入成功: {Address} = {Value}", address, writeValue);
        return CreateSuccessOutput(writeValue, dataType);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var ipAddress = GetStringParam(@operator, "IpAddress", "");
        var port = GetIntParam(@operator, "Port", 5002);
        var address = GetStringParam(@operator, "Address", "");
        var length = GetIntParam(@operator, "Length", 1);
        var pollingMode = GetStringParam(@operator, "PollingMode", "None");

        if (string.IsNullOrWhiteSpace(ipAddress))
            return ValidationResult.Invalid("IP地址不能为空");

        if (port < 1 || port > 65535)
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");

        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Invalid("PLC地址不能为空");

        if (length < 1 || length > 960)
            return ValidationResult.Invalid("读取长度必须在 1-960 之间");

        // 【第二优先级】验证轮询模式参数
        var validPollingModes = new[] { "None", "WaitForValue" };
        if (!validPollingModes.Contains(pollingMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"轮询模式必须是以下之一: {string.Join(", ", validPollingModes)}");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// 执行带轮询等待的读取操作
    /// </summary>
    private async Task<OperatorExecutionOutput> ExecuteReadWithPollingAsync(
        IPlcClient client, string address, string dataType, ushort length,
        string pollingCondition, string pollingValue, int timeoutMs, int intervalMs, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        int readCount = 0;

        Logger.LogInformation("[MitsubishiMC] 开始轮询等待: Address={Address}, Condition={Condition}, TargetValue={Target}, Timeout={Timeout}ms",
            address, pollingCondition, pollingValue, timeoutMs);

        while (true)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (elapsed > timeoutMs)
            {
                Logger.LogWarning("[MitsubishiMC] 轮询等待超时: Address={Address}, 已等待{Elapsed}ms", address, (int)elapsed);
                return CreateFailureOutput($"轮询等待超时: 等待{pollingCondition} {pollingValue}超过{timeoutMs}ms");
            }

            ct.ThrowIfCancellationRequested();

            var result = await client.ReadAsync(address, length, ct);

            if (!result.IsSuccess)
            {
                Logger.LogWarning("[MitsubishiMC] 轮询读取失败: {Message}", result.Message);
                await Task.Delay(Math.Min(intervalMs, 1000), ct);
                continue;
            }

            var currentValue = ConvertBytesToValue(result.Content!, dataType);
            readCount++;

            if (EvaluatePollingCondition(currentValue, pollingCondition, pollingValue))
            {
                var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Logger.LogInformation("[MitsubishiMC] 轮询等待完成: Address={Address}, Value={Value}, 读取{Count}次, 耗时{Elapsed}ms",
                    address, currentValue, readCount, (int)totalElapsed);

                var output = CreateSuccessOutput(currentValue, dataType);
                output.OutputData["PollingReadCount"] = readCount;
                output.OutputData["PollingElapsedMs"] = (int)totalElapsed;
                output.OutputData["PollingMatched"] = true;
                return output;
            }

            await Task.Delay(intervalMs, ct);
        }
    }

    /// <summary>
    /// 评估轮询条件
    /// </summary>
    private bool EvaluatePollingCondition(object currentValue, string condition, string targetValue)
    {
        var currentStr = currentValue?.ToString() ?? "";
        bool currentIsNumeric = double.TryParse(currentStr, out var currentNum);
        bool targetIsNumeric = double.TryParse(targetValue, out var targetNum);

        return condition.ToLower() switch
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

    /// <summary>
    /// 解析写入值：优先从上游输入获取，否则使用参数面板静态值
    /// </summary>
    private string ResolveWriteValue(Operator @operator, Dictionary<string, object>? inputs)
    {
        // 获取参数面板中的静态值（作为fallback）
        var staticValue = GetStringParam(@operator, "WriteValue", "");

        if (inputs == null || inputs.Count == 0)
            return staticValue;

        // 按优先级顺序尝试从inputs获取动态值
        // 优先级：JudgmentValue > Value > Data > 静态值
        var priorityKeys = new[] { "JudgmentValue", "Value", "Data" };

        foreach (var key in priorityKeys)
        {
            if (inputs.TryGetValue(key, out var value) && value != null)
            {
                var stringValue = value.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    Logger.LogDebug("[MitsubishiMC] 从上游获取动态值: Key={Key}, Value={Value}", key, stringValue);
                    return stringValue;
                }
            }
        }

        return staticValue;
    }
}
