using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;
using S7.Net;

namespace Acme.PlcComm.Siemens;

/// <summary>
/// 西门子S7协议客户端实现
/// 基于S7NetPlus库的封装
/// </summary>
public class SiemensS7Client : PlcBaseClient
{
    private S7.Net.Plc? _s7Plc;
    private readonly S7AddressParser _addressParser;
    private readonly SiemensCpuType _cpuType;
    private readonly int _rack;
    private readonly int _slot;

    public override int DefaultPort => 102;

    /// <summary>
    /// CPU类型
    /// </summary>
    public SiemensCpuType CpuType => _cpuType;

    /// <summary>
    /// Rack号
    /// </summary>
    public int Rack => _rack;

    /// <summary>
    /// Slot号
    /// </summary>
    public int Slot => _slot;

    public SiemensS7Client(
        string ipAddress,
        SiemensCpuType cpuType = SiemensCpuType.S71200,
        int rack = 0,
        int slot = 1,
        ILogger? logger = null)
        : base(logger)
    {
        IpAddress = ipAddress;
        _cpuType = cpuType;
        _rack = rack;
        _slot = slot;

        _addressParser = new S7AddressParser();
        ByteTransform = BigEndianTransform.Instance;
    }

    protected override async Task<bool> ConnectCoreAsync(CancellationToken ct)
    {
        try
        {
            var s7CpuType = MapCpuType(_cpuType);
            _s7Plc = new S7.Net.Plc(s7CpuType, IpAddress, (short)_rack, (short)_slot);

            await _s7Plc.OpenAsync(ct);

            if (_s7Plc.IsConnected)
            {
                _logger.LogInformation(
                    "[SiemensS7] 协议握手成功, CPU={CpuType}, Rack={Rack}, Slot={Slot}",
                    _cpuType, _rack, _slot);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SiemensS7] 协议握手失败: {Message}", ex.Message);
            return false;
        }
    }

    protected override Task DisconnectCoreAsync()
    {
        try
        {
            _s7Plc?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SiemensS7] 断开连接时发生异常: {Message}", ex.Message);
        }
        return Task.CompletedTask;
    }

    protected override async Task<OperateResult<byte[]>> ReadCoreAsync(
        string address, ushort length, CancellationToken ct)
    {
        try
        {
            if (_s7Plc == null || !_s7Plc.IsConnected)
                return OperateResult<byte[]>.Failure("PLC未连接");

            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult<byte[]>.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;
            byte[]? result;

            if (plcAddress.AreaType == "DB")
            {
                result = await _s7Plc.ReadBytesAsync(
                    DataType.DataBlock,
                    plcAddress.DbNumber,
                    plcAddress.StartAddress,
                    GetDataLength(plcAddress.DataType, length),
                    ct);
            }
            else
            {
                var dataType = GetS7DataType(plcAddress.AreaType);
                result = await _s7Plc.ReadBytesAsync(
                    dataType, 0, plcAddress.StartAddress,
                    GetDataLength(plcAddress.DataType, length), ct);
            }

            if (result == null)
                return OperateResult<byte[]>.Failure("读取返回空数据");

            return OperateResult<byte[]>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SiemensS7] 读取失败: {Message}", ex.Message);
            return OperateResult<byte[]>.Failure($"读取失败: {ex.Message}");
        }
    }

    protected override async Task<OperateResult> WriteCoreAsync(
        string address, byte[] value, CancellationToken ct)
    {
        try
        {
            if (_s7Plc == null || !_s7Plc.IsConnected)
                return OperateResult.Failure("PLC未连接");

            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;

            if (plcAddress.AreaType == "DB")
            {
                await _s7Plc.WriteBytesAsync(
                    DataType.DataBlock, plcAddress.DbNumber,
                    plcAddress.StartAddress, value, ct);
            }
            else
            {
                var dataType = GetS7DataType(plcAddress.AreaType);
                await _s7Plc.WriteBytesAsync(
                    dataType, 0, plcAddress.StartAddress, value, ct);
            }

            return OperateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SiemensS7] 写入失败: {Message}", ex.Message);
            return OperateResult.Failure($"写入失败: {ex.Message}");
        }
    }

    protected override async Task<bool> PingCoreAsync(CancellationToken ct)
    {
        try
        {
            if (_s7Plc == null || !_s7Plc.IsConnected)
                return false;

            // 使用 MW0 (Merker区) 而非 DB1.DBW0，因为所有 S7 CPU 型号都有 M 区
            var result = await ReadAsync("MW0", 2, ct);
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    private static S7.Net.CpuType MapCpuType(SiemensCpuType cpuType)
    {
        return cpuType switch
        {
            SiemensCpuType.S7200 => S7.Net.CpuType.S7200,
            SiemensCpuType.S7200Smart => S7.Net.CpuType.S7200Smart,
            SiemensCpuType.S7300 => S7.Net.CpuType.S7300,
            SiemensCpuType.S7400 => S7.Net.CpuType.S7400,
            SiemensCpuType.S71200 => S7.Net.CpuType.S71200,
            SiemensCpuType.S71500 => S7.Net.CpuType.S71500,
            _ => S7.Net.CpuType.S71200
        };
    }

    private static int GetDataLength(PlcDataType dataType, ushort count)
    {
        var typeSize = dataType switch
        {
            PlcDataType.Bit => 1,
            PlcDataType.Byte => 1,
            PlcDataType.Word or PlcDataType.Int16 => 2,
            PlcDataType.DWord or PlcDataType.Int32 or PlcDataType.Float => 4,
            PlcDataType.LWord or PlcDataType.Double => 8,
            _ => 2
        };
        return typeSize * count;
    }

    private static S7.Net.DataType GetS7DataType(string areaType)
    {
        return areaType.ToUpper() switch
        {
            "M" => S7.Net.DataType.Memory,
            "I" or "E" => S7.Net.DataType.Input,
            "Q" or "A" => S7.Net.DataType.Output,
            "T" => S7.Net.DataType.Timer,
            "C" => S7.Net.DataType.Counter,
            _ => S7.Net.DataType.Memory
        };
    }
}
