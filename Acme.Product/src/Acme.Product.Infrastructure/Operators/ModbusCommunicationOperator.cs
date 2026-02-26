// ModbusCommunicationOperator.cs
// 异步执行TCP Modbus通信（带连接池）
// 作者：蘅芜君

using System.Collections.Concurrent;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net.Sockets;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Modbus通信算子 - 支持TCP和RTU协议（带连接池）
/// </summary>
[OperatorMeta(
    DisplayName = "Modbus通信",
    Description = "工业设备Modbus RTU/TCP通信",
    Category = "通信",
    IconName = "modbus",
    Keywords = new[] { "Modbus", "PLC", "通信", "寄存器", "RTU", "TCP", "工业", "Communication" }
)]
[InputPort("Data", "数据", PortDataType.Any, IsRequired = false)]
[OutputPort("Response", "响应", PortDataType.String)]
[OutputPort("Status", "状态", PortDataType.Boolean)]
[OperatorParam("Protocol", "协议", "enum", DefaultValue = "TCP", Options = new[] { "TCP|TCP", "RTU|RTU" })]
[OperatorParam("IpAddress", "IP地址", "string", DefaultValue = "192.168.1.1")]
[OperatorParam("Port", "端口", "int", DefaultValue = 502, Min = 1, Max = 65535)]
[OperatorParam("SlaveId", "从机ID", "int", DefaultValue = 1, Min = 1, Max = 247)]
[OperatorParam("RegisterAddress", "寄存器地址", "int", DefaultValue = 0)]
[OperatorParam("RegisterCount", "寄存器数量", "int", DefaultValue = 1, Min = 1, Max = 125)]
[OperatorParam("FunctionCode", "功能码", "enum", DefaultValue = "ReadHolding", Options = new[] { "ReadCoils|读线圈", "ReadHolding|读保持寄存器", "WriteSingle|写单寄存器", "WriteMultiple|写多寄存器" })]
[OperatorParam("WriteValue", "写入值", "string", DefaultValue = "")]
public class ModbusCommunicationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ModbusCommunication;

    public ModbusCommunicationOperator(ILogger<ModbusCommunicationOperator> logger) : base(logger) { }

    // 连接池 - 静态缓存（Singleton 算子可安全使用）
    private static readonly ConcurrentDictionary<string, TcpClient> _connectionPool = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new();
    private static readonly ConcurrentDictionary<string, IModbusMaster> _masterPool = new();
    
    // Modbus 工厂
    private static readonly IModbusFactory _modbusFactory = new ModbusFactory();

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取输入数据（可选）
        object? inputData = null;
        if (inputs != null && inputs.TryGetValue("Data", out var data))
        {
            inputData = data;
        }

        // 获取参数
        var protocol = GetStringParam(@operator, "Protocol", "TCP");
        var ipAddress = GetStringParam(@operator, "IpAddress", "192.168.1.1");
        var port = GetIntParam(@operator, "Port", 502, 1, 65535);
        var slaveId = GetIntParam(@operator, "SlaveId", 1, 1, 247);
        var registerAddress = GetIntParam(@operator, "RegisterAddress", 0);
        var registerCount = GetIntParam(@operator, "RegisterCount", 1, 1, 125);
        var functionCode = GetStringParam(@operator, "FunctionCode", "ReadHolding");
        var writeValue = GetStringParam(@operator, "WriteValue", "");

        string response = "";
        bool status = false;

        if (protocol == "TCP")
        {
            (response, status) = await ExecuteTcpModbusAsync(
                ipAddress, port, slaveId, functionCode, registerAddress, registerCount, writeValue, cancellationToken);
        }
        else
        {
            // RTU协议需要串口，这里返回提示
            response = "RTU协议需要串口配置，当前版本暂不支持";
            status = false;
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Response", response },
            { "Status", status },
            { "Protocol", protocol },
            { "FunctionCode", functionCode },
            { "SlaveId", slaveId }
        });
    }

    /// <summary>
    /// 从连接池获取或创建连接
    /// </summary>
    private async Task<IModbusMaster> GetOrCreateConnectionAsync(
        string ipAddress, int port, int timeoutMs, CancellationToken ct)
    {
        var key = $"{ipAddress}:{port}";
        var lockObj = _connectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        
        await lockObj.WaitAsync(ct);
        try
        {
            // 检查现有连接是否有效
            if (_masterPool.TryGetValue(key, out var existingMaster) && 
                _connectionPool.TryGetValue(key, out var existingClient))
            {
                if (IsConnectionAlive(existingClient))
                {
                    Logger.LogDebug("Modbus 连接复用: {Key}", key);
                    return existingMaster;
                }
                
                // 清理旧连接
                try { existingClient.Close(); } catch { }
                _connectionPool.TryRemove(key, out _);
                try { existingMaster.Dispose(); } catch { }
                _masterPool.TryRemove(key, out _);
            }
            
            // 建立新连接
            var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            
            await client.ConnectAsync(ipAddress, port, cts.Token);
            
            // 使用 NModbus 工厂创建 master
            var master = _modbusFactory.CreateMaster(client);
            
            _connectionPool[key] = client;
            _masterPool[key] = master;
            
            Logger.LogInformation("Modbus 连接已建立: {Key}", key);
            return master;
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>
    /// 检测连接是否存活
    /// </summary>
    private bool IsConnectionAlive(TcpClient client)
    {
        try
        {
            if (!client.Connected) return false;
            // 通过 Poll 检测连接状态
            return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 异步执行TCP Modbus通信（带连接池）
    /// </summary>
    private async Task<(string response, bool status)> ExecuteTcpModbusAsync(
        string ipAddress, int port, int slaveId, string functionCode, int registerAddress, int registerCount, string writeValue, CancellationToken cancellationToken)
    {
        try
        {
            // 从连接池获取连接
            var master = await GetOrCreateConnectionAsync(ipAddress, port, 5000, cancellationToken);

            string response = "";
            bool status = false;

            switch (functionCode)
            {
                case "ReadCoils":
                    var coils = master.ReadCoils((byte)slaveId, (ushort)registerAddress, (ushort)registerCount);
                    response = string.Join(", ", coils);
                    status = true;
                    break;

                case "ReadHolding":
                    var registers = master.ReadHoldingRegisters((byte)slaveId, (ushort)registerAddress, (ushort)registerCount);
                    response = string.Join(", ", registers);
                    status = true;
                    break;

                case "WriteSingle":
                    if (ushort.TryParse(writeValue, out var singleValue))
                    {
                        master.WriteSingleRegister((byte)slaveId, (ushort)registerAddress, singleValue);
                        response = $"写入成功: {singleValue}";
                        status = true;
                    }
                    else
                    {
                        response = "写入值格式无效";
                    }
                    break;

                case "WriteMultiple":
                    var values = writeValue.Split(',').Select(v => ushort.TryParse(v.Trim(), out var val) ? val : (ushort)0).ToArray();
                    master.WriteMultipleRegisters((byte)slaveId, (ushort)registerAddress, values);
                    response = $"写入成功: {values.Length}个寄存器";
                    status = true;
                    break;

                default:
                    response = $"未知功能码: {functionCode}";
                    break;
            }

            return (response, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ("通信被取消", false);
        }
        catch (OperationCanceledException)
        {
            return ("连接超时", false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Modbus 通信错误: {IpAddress}:{Port}", ipAddress, port);
            return ($"通信错误: {ex.Message}", false);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var port = GetIntParam(@operator, "Port", 502);
        var slaveId = GetIntParam(@operator, "SlaveId", 1);
        var registerCount = GetIntParam(@operator, "RegisterCount", 1);
        var protocol = GetStringParam(@operator, "Protocol", "TCP");

        if (port < 1 || port > 65535)
        {
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");
        }
        if (slaveId < 1 || slaveId > 247)
        {
            return ValidationResult.Invalid("从机ID必须在 1-247 之间");
        }
        if (registerCount < 1 || registerCount > 125)
        {
            return ValidationResult.Invalid("寄存器数量必须在 1-125 之间");
        }
        if (protocol != "TCP" && protocol != "RTU")
        {
            return ValidationResult.Invalid("协议必须是 TCP 或 RTU");
        }

        return ValidationResult.Valid();
    }
}
