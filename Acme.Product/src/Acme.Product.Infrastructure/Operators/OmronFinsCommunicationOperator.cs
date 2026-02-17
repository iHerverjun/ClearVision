using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 欧姆龙FINS通信算子
/// 支持FINS/TCP协议，适用于CP1H/CJ2M/NJ/NX系列PLC
/// </summary>
public class OmronFinsCommunicationOperator : PlcCommunicationOperatorBase
{
    public override OperatorType OperatorType => OperatorType.OmronFinsCommunication;

    public OmronFinsCommunicationOperator(ILogger<OmronFinsCommunicationOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var ipAddress = GetStringParam(@operator, "IpAddress", "192.168.250.1");
        var port = GetIntParam(@operator, "Port", 9600, 1, 65535);
        var address = GetStringParam(@operator, "Address", "DM100");
        var length = GetIntParam(@operator, "Length", 1, 1, 999);
        var dataType = GetStringParam(@operator, "DataType", "Word");
        var operation = GetStringParam(@operator, "Operation", "Read");
        
        // 【增强】支持从上游输入动态获取写入值
        var writeValue = ResolveWriteValue(@operator, inputs);

        // 构建连接键
        var connectionKey = $"FINS:{ipAddress}:{port}";

        try
        {
            // 获取或创建连接
            var (client, isNew) = await GetOrCreateConnectionAsync(connectionKey, () =>
            {
                var finsClient = PlcClientFactory.CreateOmronFins(ipAddress);
                ((Acme.PlcComm.Omron.OmronFinsClient)finsClient).Port = port;
                return finsClient;
            });

            if (operation.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteReadAsync(client, address, dataType, (ushort)length, cancellationToken);
            }
            else
            {
                return await ExecuteWriteAsync(client, address, dataType, writeValue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OmronFINS] 通信错误: {IP}:{Port} - {Message}", ipAddress, port, ex.Message);
            return CreateFailureOutput($"FINS通信错误: {ex.Message}");
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteReadAsync(
        IPlcClient client, string address, string dataType, ushort length, CancellationToken ct)
    {
        var result = await client.ReadAsync(address, length, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"读取失败: {result.Message}");

        var value = ConvertBytesToValue(result.Content!, dataType);
        Logger.LogInformation("[OmronFINS] 读取成功: {Address} = {Value}", address, value);
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

        Logger.LogInformation("[OmronFINS] 写入成功: {Address} = {Value}", address, writeValue);
        return CreateSuccessOutput(writeValue, dataType);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var ipAddress = GetStringParam(@operator, "IpAddress", "");
        var port = GetIntParam(@operator, "Port", 9600);
        var address = GetStringParam(@operator, "Address", "");
        var length = GetIntParam(@operator, "Length", 1);

        if (string.IsNullOrWhiteSpace(ipAddress))
            return ValidationResult.Invalid("IP地址不能为空");

        if (port < 1 || port > 65535)
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");

        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Invalid("PLC地址不能为空");

        if (length < 1 || length > 999)
            return ValidationResult.Invalid("读取长度必须在 1-999 之间");

        return ValidationResult.Valid();
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
                    Logger.LogDebug("[OmronFINS] 从上游获取动态值: Key={Key}, Value={Value}", key, stringValue);
                    return stringValue;
                }
            }
        }

        return staticValue;
    }
}
