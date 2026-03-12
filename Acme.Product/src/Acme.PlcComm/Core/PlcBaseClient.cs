// PlcBaseClient.cs
// PLC客户端抽象基类
// 作者：蘅芜君

using System.Net.Sockets;
using System.IO;
using System.Text;
using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.PlcComm.Core;

/// <summary>
/// PLC客户端抽象基类
/// 封装了连接管理、重连机制、线程同步等通用功能
/// </summary>
public abstract class PlcBaseClient : IPlcClient
{
    // ─── 线程安全：单连接互斥锁 ────────────────────────────
    // PLC协议通常为半双工，必须保证Request-Response串行
    protected readonly SemaphoreSlim _communicationLock = new(1, 1);
    protected readonly SemaphoreSlim _connectLock = new(1, 1);
    
    // ─── 网络连接 ──────────────────────────────────────────
    protected TcpClient? _tcpClient;
    protected NetworkStream? _networkStream;
    protected DateTime _lastCommunicationTime = DateTime.MinValue;
    protected bool _disposed;

    // ─── 日志 ──────────────────────────────────────────────
    protected readonly ILogger _logger;

    // ─── 属性 ──────────────────────────────────────────────
    public string IpAddress { get; protected set; } = "192.168.0.1";
    public int Port { get; set; }
    public abstract int DefaultPort { get; }
    
    public virtual bool IsConnected
    {
        get
        {
            if (_disposed) return false;
            if (_tcpClient?.Connected != true) return false;
            // 检查最后通信时间，超过60秒认为可能断开
            if (DateTime.Now - _lastCommunicationTime > TimeSpan.FromSeconds(60))
                return false;
            return true;
        }
    }

    public int ConnectTimeout { get; set; } = 10000;
    public int ReadTimeout { get; set; } = 5000;
    public int WriteTimeout { get; set; } = 5000;
    public ReconnectPolicy ReconnectPolicy { get; set; } = new();

    // ─── 字节序转换器（子类注入） ──────────────────────────
    public IByteTransform ByteTransform { get; protected set; } = BigEndianTransform.Instance;

    // ─── 事件 ──────────────────────────────────────────────
    public event EventHandler<ConnectionEventArgs>? Connected;
    public event EventHandler<DisconnectionEventArgs>? Disconnected;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    protected PlcBaseClient(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    // ─── 生命周期 ───────────────────────────────────────────
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        
        await _connectLock.WaitAsync(ct);
        try
        {
            if (IsConnected)
            {
                _logger.LogDebug("[{ClientType}] 已经连接，跳过重复连接", GetType().Name);
                return true;
            }

            // 自动设置默认端口
            if (Port == 0) Port = DefaultPort;

            _logger.LogInformation("[{ClientType}] 正在连接 {Ip}:{Port}...", GetType().Name, IpAddress, Port);

            try
            {
                // TCP连接
                _tcpClient = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ConnectTimeout);
                await _tcpClient.ConnectAsync(IpAddress, Port, cts.Token);
                _networkStream = _tcpClient.GetStream();
                _networkStream.ReadTimeout = ReadTimeout;
                _networkStream.WriteTimeout = WriteTimeout;

                // 协议特定的握手
                var result = await ConnectCoreAsync(ct);
                if (!result)
                {
                    await DisconnectInternalAsync(
                        DisconnectionReason.ProtocolError,
                        "协议握手失败，已关闭连接资源");
                    RaiseError(-1, "协议握手失败", "Connect");
                    return false;
                }

                _lastCommunicationTime = DateTime.Now;
                
                // 触发事件
                Connected?.Invoke(this, new ConnectionEventArgs
                {
                    IpAddress = IpAddress,
                    Port = Port,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("[{ClientType}] 连接成功", GetType().Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ClientType}] 连接失败: {Message}", GetType().Name, ex.Message);
                await DisconnectInternalAsync(
                    DisconnectionReason.NetworkError,
                    $"连接失败: {ex.Message}");
                RaiseError(-1, $"连接失败: {ex.Message}", "Connect");
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed && !HasActiveConnectionResources()) return;

        await _connectLock.WaitAsync();
        try
        {
            if (!HasActiveConnectionResources())
            {
                _logger.LogDebug("[{ClientType}] 已无活动连接，跳过重复断开", GetType().Name);
                return;
            }

            await DisconnectInternalAsync(
                DisconnectionReason.UserInitiated,
                "用户主动断开连接");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    // ─── 读写操作 ───────────────────────────────────────────
    public async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken ct = default)
    {
        return await ExecuteWithReconnectAsync(async () =>
        {
            if (!IsConnected)
            {
                const string message = "PLC未连接";
                RaiseError(-1, message, "Read");
                return OperateResult<byte[]>.Failure(message);
            }

            var result = await ReadCoreAsync(address, length, ct);
            if (!result.IsSuccess)
                RaiseError(result, "Read");
            if (result.IsSuccess)
                _lastCommunicationTime = DateTime.Now;
            return result;
        }, ct);
    }

    public async Task<OperateResult> WriteAsync(string address, byte[] value, CancellationToken ct = default)
    {
        return await ExecuteWithReconnectAsync(async () =>
        {
            if (!IsConnected)
            {
                const string message = "PLC未连接";
                RaiseError(-1, message, "Write");
                return OperateResult.Failure(message);
            }

            var result = await WriteCoreAsync(address, value, ct);
            if (!result.IsSuccess)
                RaiseError(result, "Write");
            if (result.IsSuccess)
                _lastCommunicationTime = DateTime.Now;
            return result;
        }, ct);
    }

    public async Task<OperateResult<T>> ReadAsync<T>(string address, CancellationToken ct = default) where T : struct
    {
        try
        {
            var (length, dataType) = GetTypeInfo<T>();
            
            var result = await ReadAsync(address, length, ct);
            if (!result.IsSuccess)
                return OperateResult<T>.Failure(result.ErrorCode, result.Message);

            var value = ConvertBytesToType<T>(result.Content!, dataType);
            return OperateResult<T>.Success(value);
        }
        catch (Exception ex)
        {
            RaiseError(-1, $"读取类型数据失败: {ex.Message}", "ReadTyped");
            return OperateResult<T>.Failure($"读取类型数据失败: {ex.Message}");
        }
    }

    public async Task<OperateResult> WriteAsync<T>(string address, T value, CancellationToken ct = default) where T : struct
    {
        try
        {
            var (length, _) = GetTypeInfo<T>();
            var bytes = ConvertTypeToBytes(value);
            return await WriteAsync(address, bytes, ct);
        }
        catch (Exception ex)
        {
            RaiseError(-1, $"写入类型数据失败: {ex.Message}", "WriteTyped");
            return OperateResult.Failure($"写入类型数据失败: {ex.Message}");
        }
    }

    public async Task<OperateResult<Dictionary<string, byte[]>>> ReadBatchAsync(
        string[] addresses, ushort[] lengths, CancellationToken ct = default)
    {
        if (addresses.Length != lengths.Length)
            return OperateResult<Dictionary<string, byte[]>>.Failure("地址数组和长度数组长度不匹配");

        var results = new Dictionary<string, byte[]>();
        
        for (int i = 0; i < addresses.Length; i++)
        {
            var result = await ReadAsync(addresses[i], lengths[i], ct);
            if (!result.IsSuccess)
                return OperateResult<Dictionary<string, byte[]>>.Failure(result.ErrorCode, result.Message);
            
            results[addresses[i]] = result.Content!;
        }

        return OperateResult<Dictionary<string, byte[]>>.Success(results);
    }

    public async Task<OperateResult<string>> ReadStringAsync(string address, ushort length, CancellationToken ct = default)
    {
        var result = await ReadAsync(address, length, ct);
        if (!result.IsSuccess)
            return OperateResult<string>.Failure(result.ErrorCode, result.Message);

        var str = ByteTransform.ToString(result.Content!, 0, length, Encoding.ASCII);
        return OperateResult<string>.Success(str);
    }

    public async Task<OperateResult> WriteStringAsync(string address, string value, CancellationToken ct = default)
    {
        var bytes = ByteTransform.GetBytes(value, value.Length, Encoding.ASCII);
        return await WriteAsync(address, bytes, ct);
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return false;
        
        try
        {
            // 子类可以实现特定的心跳检测
            return await PingCoreAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    // ─── 自动重连模板方法 ──────────────────────────────────
    protected async Task<OperateResult> ExecuteWithReconnectAsync(
        Func<Task<OperateResult>> operation, CancellationToken ct)
    {
        if (!ReconnectPolicy.Enabled)
        {
            await _communicationLock.WaitAsync(ct);
            try { return await operation(); }
            finally { _communicationLock.Release(); }
        }

        for (int retry = 0; retry <= ReconnectPolicy.MaxRetries; retry++)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[{ClientType}] 连接断开，尝试重连... (第{Retry}次)", GetType().Name, retry + 1);
                var connected = await ConnectAsync(ct);
                if (!connected)
                {
                    if (retry < ReconnectPolicy.MaxRetries)
                    {
                        var delay = GetRetryDelay(retry);
                        await Task.Delay(delay, ct);
                        continue;
                    }
                    RaiseError(-1, "重连失败，超过最大重试次数", "Reconnect");
                    return OperateResult.Failure("重连失败，超过最大重试次数");
                }
            }

            try
            {
                await _communicationLock.WaitAsync(ct);
                try { return await operation(); }
                finally { _communicationLock.Release(); }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[{ClientType}] 通信IO异常，准备重试", GetType().Name);
                var message = $"通信IO异常: {ex.Message}";
                RaiseError(-1, message, "Communication");
                await DisconnectAsync();
                return OperateResult.Failure(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ClientType}] 执行操作异常", GetType().Name);
                RaiseError(-1, $"执行失败: {ex.Message}", "Operation");
                return OperateResult.Failure($"执行失败: {ex.Message}");
            }
        }

        RaiseError(-1, "超过最大重试次数", "Reconnect");
        return OperateResult.Failure("超过最大重试次数");
    }

    protected async Task<OperateResult<T>> ExecuteWithReconnectAsync<T>(
        Func<Task<OperateResult<T>>> operation, CancellationToken ct)
    {
        if (!ReconnectPolicy.Enabled)
        {
            await _communicationLock.WaitAsync(ct);
            try { return await operation(); }
            finally { _communicationLock.Release(); }
        }

        for (int retry = 0; retry <= ReconnectPolicy.MaxRetries; retry++)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[{ClientType}] 连接断开，尝试重连... (第{Retry}次)", GetType().Name, retry + 1);
                var connected = await ConnectAsync(ct);
                if (!connected)
                {
                    if (retry < ReconnectPolicy.MaxRetries)
                    {
                        await Task.Delay(GetRetryDelay(retry), ct);
                        continue;
                    }

                    RaiseError(-1, "重连失败，超过最大重试次数", "Reconnect");
                    return OperateResult<T>.Failure("重连失败，超过最大重试次数");
                }
            }

            try
            {
                await _communicationLock.WaitAsync(ct);
                try { return await operation(); }
                finally { _communicationLock.Release(); }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[{ClientType}] 通信IO异常，准备重试", GetType().Name);
                var message = $"通信IO异常: {ex.Message}";
                RaiseError(-1, message, "Communication");
                await DisconnectAsync();
                return OperateResult<T>.Failure(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ClientType}] 执行操作异常", GetType().Name);
                RaiseError(-1, $"执行失败: {ex.Message}", "Operation");
                return OperateResult<T>.Failure($"执行失败: {ex.Message}");
            }
        }

        RaiseError(-1, "超过最大重试次数", "Reconnect");
        return OperateResult<T>.Failure("超过最大重试次数");
    }

    protected static async Task<bool> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read == 0)
                return false;

            totalRead += read;
        }

        return true;
    }

    // ─── 报文日志 ──────────────────────────────────────────
    protected void LogFrame(string direction, byte[] data)
    {
        if (data == null || data.Length == 0) return;
        
        var hexString = BitConverter.ToString(data).Replace("-", " ");
        _logger.LogDebug("[{ClientType}] [{Direction}] {Hex}", GetType().Name, direction, hexString);
    }

    // ─── 辅助方法 ──────────────────────────────────────────
    protected void RaiseError(OperateResult result, string operationType)
    {
        if (!result.IsSuccess)
            RaiseError(result.ErrorCode, result.Message, operationType);
    }

    protected void RaiseError(int errorCode, string message, string operationType)
    {
        ErrorOccurred?.Invoke(this, new PlcErrorEventArgs
        {
            Timestamp = DateTime.Now,
            ErrorCode = errorCode,
            Message = message,
            OperationType = operationType
        });
    }

    private TimeSpan GetRetryDelay(int retry)
    {
        var calculatedDelay = ReconnectPolicy.ExponentialBackoff
            ? TimeSpan.FromSeconds(Math.Pow(2, retry))
            : ReconnectPolicy.RetryInterval;

        return calculatedDelay <= ReconnectPolicy.MaxRetryInterval
            ? calculatedDelay
            : ReconnectPolicy.MaxRetryInterval;
    }

    protected virtual bool HasActiveConnectionResources()
    {
        return _tcpClient != null || _networkStream != null;
    }

    private async Task DisconnectInternalAsync(DisconnectionReason reason, string message)
    {
        _logger.LogInformation("[{ClientType}] 正在断开连接...", GetType().Name);

        try
        {
            await DisconnectCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{ClientType}] 协议层断开连接异常: {Message}", GetType().Name, ex.Message);
            RaiseError(-1, $"协议层断开连接异常: {ex.Message}", "Disconnect");
        }

        try
        {
            _networkStream?.Close();
            _networkStream?.Dispose();
            _networkStream = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{ClientType}] 关闭网络流异常: {Message}", GetType().Name, ex.Message);
            RaiseError(-1, $"关闭网络流异常: {ex.Message}", "Disconnect");
        }

        try
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{ClientType}] 关闭TCP连接异常: {Message}", GetType().Name, ex.Message);
            RaiseError(-1, $"关闭TCP连接异常: {ex.Message}", "Disconnect");
        }

        Disconnected?.Invoke(this, new DisconnectionEventArgs
        {
            Timestamp = DateTime.Now,
            Reason = reason,
            Message = message
        });

        _logger.LogInformation("[{ClientType}] 已断开连接", GetType().Name);
    }

    private static (ushort length, PlcDataType dataType) GetTypeInfo<T>()
    {
        var type = typeof(T);
        return type.Name switch
        {
            "Boolean" or "Bool" => (1, PlcDataType.Bit),
            "Byte" => (1, PlcDataType.Byte),
            "Int16" or "UInt16" or "Short" or "UShort" => (2, PlcDataType.Word),
            "Int32" or "UInt32" or "Int" or "UInt" => (4, PlcDataType.DWord),
            "Single" or "Float" => (4, PlcDataType.Float),
            "Double" => (8, PlcDataType.Double),
            "Int64" or "UInt64" or "Long" or "ULong" => (8, PlcDataType.LWord),
            _ => throw new NotSupportedException($"不支持的数据类型: {type.Name}")
        };
    }

    private T ConvertBytesToType<T>(byte[] buffer, PlcDataType dataType) where T : struct
    {
        var type = typeof(T);
        object value = type.Name switch
        {
            "Boolean" or "Bool" => ByteTransform.ToBool(buffer, 0),
            "Byte" => buffer[0],
            "Int16" or "Short" => ByteTransform.ToInt16(buffer, 0),
            "UInt16" or "UShort" => ByteTransform.ToUInt16(buffer, 0),
            "Int32" or "Int" => ByteTransform.ToInt32(buffer, 0),
            "UInt32" or "UInt" => ByteTransform.ToUInt32(buffer, 0),
            "Single" or "Float" => ByteTransform.ToFloat(buffer, 0),
            "Double" => ByteTransform.ToDouble(buffer, 0),
            "Int64" or "Long" => ByteTransform.ToInt64(buffer, 0),
            "UInt64" or "ULong" => ByteTransform.ToUInt64(buffer, 0),
            _ => throw new NotSupportedException($"不支持的数据类型: {type.Name}")
        };
        return (T)value;
    }

    private byte[] ConvertTypeToBytes<T>(T value) where T : struct
    {
        var type = typeof(T);
        return type.Name switch
        {
            "Boolean" or "Bool" => ByteTransform.GetBytes(Convert.ToBoolean(value)),
            "Byte" => new byte[] { Convert.ToByte(value) },
            "Int16" or "Short" => ByteTransform.GetBytes(Convert.ToInt16(value)),
            "UInt16" or "UShort" => ByteTransform.GetBytes(Convert.ToUInt16(value)),
            "Int32" or "Int" => ByteTransform.GetBytes(Convert.ToInt32(value)),
            "UInt32" or "UInt" => ByteTransform.GetBytes(Convert.ToUInt32(value)),
            "Single" or "Float" => ByteTransform.GetBytes(Convert.ToSingle(value)),
            "Double" => ByteTransform.GetBytes(Convert.ToDouble(value)),
            "Int64" or "Long" => ByteTransform.GetBytes(Convert.ToInt64(value)),
            "UInt64" or "ULong" => ByteTransform.GetBytes(Convert.ToUInt64(value)),
            _ => throw new NotSupportedException($"不支持的数据类型: {type.Name}")
        };
    }

    // ─── 子类必须实现的抽象方法 ────────────────────────────
    protected abstract Task<bool> ConnectCoreAsync(CancellationToken ct);
    protected abstract Task DisconnectCoreAsync();
    protected abstract Task<OperateResult<byte[]>> ReadCoreAsync(string address, ushort length, CancellationToken ct);
    protected abstract Task<OperateResult> WriteCoreAsync(string address, byte[] value, CancellationToken ct);
    protected virtual Task<bool> PingCoreAsync(CancellationToken ct) => Task.FromResult(IsConnected);

    // ─── IDisposable ───────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _disposed = true;
            _communicationLock.Dispose();
            _connectLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
