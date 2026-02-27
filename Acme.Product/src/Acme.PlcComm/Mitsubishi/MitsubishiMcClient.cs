// MitsubishiMcClient.cs
// 三菱MC协议客户端实现
// 作者：蘅芜君

using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Microsoft.Extensions.Logging;

namespace Acme.PlcComm.Mitsubishi;

/// <summary>
/// 三菱MC协议客户端实现
/// 原生3E帧实现
/// </summary>
public class MitsubishiMcClient : PlcBaseClient
{
    private readonly McAddressParser _addressParser;
    private readonly McFrameBuilder _frameBuilder;

    public override int DefaultPort => 5002;

    public MitsubishiMcClient(string ipAddress, ILogger? logger = null)
        : base(logger)
    {
        IpAddress = ipAddress;
        Port = DefaultPort;
        
        _addressParser = new McAddressParser();
        _frameBuilder = new McFrameBuilder();
        ByteTransform = LittleEndianTransform.Instance; // MC使用小端序
    }

    protected override async Task<bool> ConnectCoreAsync(CancellationToken ct)
    {
        // MC协议不需要特殊的握手，TCP连接成功即可
        _logger.LogInformation("[MitsubishiMC] TCP连接成功，MC协议就绪");
        return true;
    }

    protected override async Task DisconnectCoreAsync()
    {
        // TCP断开即可
        await Task.CompletedTask;
    }

    protected override async Task<OperateResult<byte[]>> ReadCoreAsync(
        string address, ushort length, CancellationToken ct)
    {
        try
        {
            // 解析地址
            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult<byte[]>.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;

            // 构建请求帧
            var isBitAccess = plcAddress.DataType == PlcDataType.Bit;
            var requestFrame = _frameBuilder.BuildReadRequest(
                plcAddress.DeviceCode,
                plcAddress.StartAddress,
                length,
                isBitAccess);

            LogFrame("TX", requestFrame);

            // 发送请求
            if (_networkStream == null)
                return OperateResult<byte[]>.Failure("网络流未初始化");

            await _networkStream.WriteAsync(requestFrame, 0, requestFrame.Length, ct);

            // 读取响应头
            var headerBuffer = new byte[11]; // 最小响应头长度
            var headerReadOk = await ReadExactAsync(_networkStream, headerBuffer, 0, headerBuffer.Length, ct);
            if (!headerReadOk)
                return OperateResult<byte[]>.Failure("读取响应头失败");

            // 计算响应数据长度
            var dataLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.AsSpan(9, 2));
            var totalResponseLength = 11 + dataLength;

            // 读取完整响应
            var responseBuffer = new byte[totalResponseLength];
            Array.Copy(headerBuffer, responseBuffer, headerBuffer.Length);
            
            var remainingBytes = totalResponseLength - headerBuffer.Length;
            if (remainingBytes > 0)
            {
                var payloadReadOk = await ReadExactAsync(
                    _networkStream, responseBuffer, headerBuffer.Length, remainingBytes, ct);
                if (!payloadReadOk)
                    return OperateResult<byte[]>.Failure("读取响应数据不完整");
            }

            LogFrame("RX", responseBuffer);

            // 解析响应
            var (success, data, error) = _frameBuilder.ParseReadResponse(responseBuffer);
            if (!success)
                return OperateResult<byte[]>.Failure(error ?? "未知错误");

            return OperateResult<byte[]>.Success(data!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MitsubishiMC] 读取失败: {Message}", ex.Message);
            return OperateResult<byte[]>.Failure($"读取失败: {ex.Message}");
        }
    }

    protected override async Task<OperateResult> WriteCoreAsync(
        string address, byte[] value, CancellationToken ct)
    {
        try
        {
            // 解析地址
            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;

            // 构建请求帧
            var isBitAccess = plcAddress.DataType == PlcDataType.Bit;
            var length = (ushort)(isBitAccess ? value.Length : value.Length / 2);
            
            var requestFrame = _frameBuilder.BuildWriteRequest(
                plcAddress.DeviceCode,
                plcAddress.StartAddress,
                length,
                value,
                isBitAccess);

            LogFrame("TX", requestFrame);

            // 发送请求
            if (_networkStream == null)
                return OperateResult.Failure("网络流未初始化");

            await _networkStream.WriteAsync(requestFrame, 0, requestFrame.Length, ct);

            // 读取响应
            var responseBuffer = new byte[11]; // 写入响应最小长度
            var responseReadOk = await ReadExactAsync(_networkStream, responseBuffer, 0, responseBuffer.Length, ct);
            if (!responseReadOk)
                return OperateResult.Failure("读取响应失败");

            LogFrame("RX", responseBuffer);

            // 解析响应
            var (success, error) = _frameBuilder.ParseWriteResponse(responseBuffer);
            if (!success)
                return OperateResult.Failure(error ?? "写入失败");

            return OperateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MitsubishiMC] 写入失败: {Message}", ex.Message);
            return OperateResult.Failure($"写入失败: {ex.Message}");
        }
    }

    protected override async Task<bool> PingCoreAsync(CancellationToken ct)
    {
        try
        {
            // 尝试读取一个字来检测连接
            var result = await ReadAsync("D0", 1, ct);
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }
}
