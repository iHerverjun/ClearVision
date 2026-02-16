using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Acme.PlcComm.Siemens;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 西门子S7通信算子
/// 支持S7-200/300/400/1200/1500系列PLC读写操作
/// </summary>
public class SiemensS7CommunicationOperator : PlcCommunicationOperatorBase
{
    public override OperatorType OperatorType => OperatorType.SiemensS7Communication;

    public SiemensS7CommunicationOperator(ILogger<SiemensS7CommunicationOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var ipAddress = GetStringParam(@operator, "IpAddress", "192.168.0.1");
        var port = GetIntParam(@operator, "Port", 102, 1, 65535);
        var cpuTypeStr = GetStringParam(@operator, "CpuType", "S71200");
        var rack = GetIntParam(@operator, "Rack", 0, 0, 15);
        var slot = GetIntParam(@operator, "Slot", 1, 0, 15);
        var address = GetStringParam(@operator, "Address", "DB1.DBW100");
        var dataType = GetStringParam(@operator, "DataType", "Word");
        var operation = GetStringParam(@operator, "Operation", "Read");
        var writeValue = GetStringParam(@operator, "WriteValue", "");

        // 解析CPU类型
        var cpuType = cpuTypeStr.ToUpper() switch
        {
            "S7200" => SiemensCpuType.S7200,
            "S7200SMART" => SiemensCpuType.S7200Smart,
            "S7300" => SiemensCpuType.S7300,
            "S7400" => SiemensCpuType.S7400,
            "S71200" => SiemensCpuType.S71200,
            "S71500" => SiemensCpuType.S71500,
            _ => SiemensCpuType.S71200
        };

        // 构建连接键
        var connectionKey = $"S7:{ipAddress}:{port}:{cpuType}:{rack}:{slot}";

        try
        {
            // 获取或创建连接
            var (client, isNew) = await GetOrCreateConnectionAsync(connectionKey, () =>
            {
                var s7Client = PlcClientFactory.CreateSiemensS7(ipAddress, cpuType, rack, slot);
                ((Acme.PlcComm.Siemens.SiemensS7Client)s7Client).Port = port;
                return s7Client;
            });

            if (operation.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteReadAsync(client, address, dataType, cancellationToken);
            }
            else
            {
                return await ExecuteWriteAsync(client, address, dataType, writeValue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[SiemensS7] 通信错误: {IP}:{Port} - {Message}", ipAddress, port, ex.Message);
            return CreateFailureOutput($"S7通信错误: {ex.Message}");
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteReadAsync(
        IPlcClient client, string address, string dataType, CancellationToken ct)
    {
        var length = GetDataLength(dataType);
        var result = await client.ReadAsync(address, length, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"读取失败: {result.Message}");

        var value = ConvertBytesToValue(result.Content!, dataType);
        Logger.LogInformation("[SiemensS7] 读取成功: {Address} = {Value}", address, value);
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

        Logger.LogInformation("[SiemensS7] 写入成功: {Address} = {Value}", address, writeValue);
        return CreateSuccessOutput(writeValue, dataType);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var ipAddress = GetStringParam(@operator, "IpAddress", "");
        var port = GetIntParam(@operator, "Port", 102);
        var address = GetStringParam(@operator, "Address", "");

        if (string.IsNullOrWhiteSpace(ipAddress))
            return ValidationResult.Invalid("IP地址不能为空");

        if (port < 1 || port > 65535)
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");

        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Invalid("PLC地址不能为空");

        return ValidationResult.Valid();
    }
}
