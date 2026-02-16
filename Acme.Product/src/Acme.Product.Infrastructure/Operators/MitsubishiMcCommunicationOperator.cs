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
        var writeValue = GetStringParam(@operator, "WriteValue", "");

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

        if (string.IsNullOrWhiteSpace(ipAddress))
            return ValidationResult.Invalid("IP地址不能为空");

        if (port < 1 || port > 65535)
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");

        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Invalid("PLC地址不能为空");

        if (length < 1 || length > 960)
            return ValidationResult.Invalid("读取长度必须在 1-960 之间");

        return ValidationResult.Valid();
    }
}
