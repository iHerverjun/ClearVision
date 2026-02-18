// OmronFinsClient.cs
// 欧姆龙FINS// 功能实现TCP协议客户端实现
// 作者：蘅芜君

using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Microsoft.Extensions.Logging;

namespace Acme.PlcComm.Omron;

/// <summary>
/// 欧姆龙FINS/TCP协议客户端实现
/// </summary>
public class OmronFinsClient : PlcBaseClient
{
    private readonly FinsAddressParser _addressParser;
    private readonly FinsFrameBuilder _frameBuilder;

    // FINS/TCP握手后获取的节点号
    private byte _clientNode = 0;
    private byte _serverNode = 0;

    public override int DefaultPort => 9600;

    public OmronFinsClient(string ipAddress, ILogger? logger = null)
        : base(logger)
    {
        IpAddress = ipAddress;
        Port = DefaultPort;
        
        _addressParser = new FinsAddressParser();
        _frameBuilder = new FinsFrameBuilder();
        ByteTransform = BigEndianTransform.Instance; // FINS使用大端序
    }

    protected override async Task<bool> ConnectCoreAsync(CancellationToken ct)
    {
        try
        {
            // FINS/TCP需要握手过程
            // 第一步：发送节点地址请求
            var nodeRequest = _frameBuilder.BuildNodeAddressRequest();
            LogFrame("TX(Handshake)", nodeRequest);

            if (_networkStream == null)
                return false;

            await _networkStream.WriteAsync(nodeRequest, 0, nodeRequest.Length, ct);

            // 读取节点地址响应
            var responseBuffer = new byte[24];
            var bytesRead = await _networkStream.ReadAsync(responseBuffer, 0, responseBuffer.Length, ct);
            if (bytesRead < 24)
            {
                _logger.LogError("[OmronFINS] 握手响应太短");
                return false;
            }

            LogFrame("RX(Handshake)", responseBuffer);

            // 解析节点地址响应
            var (success, clientNode, serverNode, error) = _frameBuilder.ParseNodeAddressResponse(responseBuffer);
            if (!success)
            {
                _logger.LogError("[OmronFINS] 握手失败: {Error}", error);
                return false;
            }

            _clientNode = clientNode;
            _serverNode = serverNode;

            _logger.LogInformation(
                "[OmronFINS] 握手成功, 客户端节点={ClientNode}, 服务器节点={ServerNode}",
                _clientNode, _serverNode);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OmronFINS] 握手异常: {Message}", ex.Message);
            return false;
        }
    }

    protected override async Task DisconnectCoreAsync()
    {
        _clientNode = 0;
        _serverNode = 0;
        await Task.CompletedTask;
    }

    protected override async Task<OperateResult<byte[]>> ReadCoreAsync(
        string address, ushort length, CancellationToken ct)
    {
        try
        {
            if (_clientNode == 0 || _serverNode == 0)
                return OperateResult<byte[]>.Failure("FINS握手未完成");

            // 解析地址
            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult<byte[]>.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;

            // 构建读取请求
            var bitAddress = (byte)(plcAddress.BitOffset >= 0 ? plcAddress.BitOffset : 0);
            var requestFrame = _frameBuilder.BuildMemoryReadRequest(
                plcAddress.DeviceCode,
                (ushort)plcAddress.StartAddress,
                bitAddress,
                length,
                _clientNode,
                _serverNode);

            LogFrame("TX", requestFrame);

            // 发送请求
            if (_networkStream == null)
                return OperateResult<byte[]>.Failure("网络流未初始化");

            await _networkStream.WriteAsync(requestFrame, 0, requestFrame.Length, ct);

            // 读取响应(先读取头部以确定总长度)
            var headerBuffer = new byte[16];
            var bytesRead = await _networkStream.ReadAsync(headerBuffer, 0, headerBuffer.Length, ct);
            if (bytesRead < 16)
                return OperateResult<byte[]>.Failure("读取响应头失败");

            // 计算剩余数据长度
            var dataLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(4, 4));
            var totalLength = 16 + (int)dataLength;
            var remainingBytes = totalLength - 16;

            // 读取完整响应
            var responseBuffer = new byte[totalLength];
            Array.Copy(headerBuffer, responseBuffer, 16);

            if (remainingBytes > 0)
            {
                bytesRead = await _networkStream.ReadAsync(
                    responseBuffer, 16, remainingBytes, ct);
                if (bytesRead < remainingBytes)
                    return OperateResult<byte[]>.Failure("读取响应数据不完整");
            }

            LogFrame("RX", responseBuffer);

            // 解析响应
            var (success, data, error) = _frameBuilder.ParseResponse(responseBuffer);
            if (!success)
                return OperateResult<byte[]>.Failure(error ?? "未知错误");

            return OperateResult<byte[]>.Success(data!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OmronFINS] 读取失败: {Message}", ex.Message);
            return OperateResult<byte[]>.Failure($"读取失败: {ex.Message}");
        }
    }

    protected override async Task<OperateResult> WriteCoreAsync(
        string address, byte[] value, CancellationToken ct)
    {
        try
        {
            if (_clientNode == 0 || _serverNode == 0)
                return OperateResult.Failure("FINS握手未完成");

            // 解析地址
            var addressResult = _addressParser.Parse(address);
            if (!addressResult.IsSuccess)
                return OperateResult.Failure(addressResult.ErrorCode, addressResult.Message);

            var plcAddress = addressResult.Content!;

            // 构建写入请求
            var bitAddress = (byte)(plcAddress.BitOffset >= 0 ? plcAddress.BitOffset : 0);
            var requestFrame = _frameBuilder.BuildMemoryWriteRequest(
                plcAddress.DeviceCode,
                (ushort)plcAddress.StartAddress,
                bitAddress,
                value,
                _clientNode,
                _serverNode);

            LogFrame("TX", requestFrame);

            // 发送请求
            if (_networkStream == null)
                return OperateResult.Failure("网络流未初始化");

            await _networkStream.WriteAsync(requestFrame, 0, requestFrame.Length, ct);

            // 读取响应
            var headerBuffer = new byte[16];
            var bytesRead = await _networkStream.ReadAsync(headerBuffer, 0, headerBuffer.Length, ct);
            if (bytesRead < 16)
                return OperateResult.Failure("读取响应头失败");

            var dataLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(4, 4));
            var totalLength = 16 + (int)dataLength;
            var remainingBytes = totalLength - 16;

            var responseBuffer = new byte[totalLength];
            Array.Copy(headerBuffer, responseBuffer, 16);

            if (remainingBytes > 0)
            {
                bytesRead = await _networkStream.ReadAsync(
                    responseBuffer, 16, remainingBytes, ct);
                if (bytesRead < remainingBytes)
                    return OperateResult.Failure("读取响应数据不完整");
            }

            LogFrame("RX", responseBuffer);

            // 解析响应
            var (success, data, error) = _frameBuilder.ParseResponse(responseBuffer);
            if (!success)
                return OperateResult.Failure(error ?? "写入失败");

            return OperateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OmronFINS] 写入失败: {Message}", ex.Message);
            return OperateResult.Failure($"写入失败: {ex.Message}");
        }
    }

    protected override async Task<bool> PingCoreAsync(CancellationToken ct)
    {
        try
        {
            // 尝试读取一个字来检测连接
            var result = await ReadAsync("DM0", 1, ct);
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }
}
