// ConnectionPoolManager.cs
// 通信连接池管理器
// 负责 Modbus/TCP 连接复用、健康检查与空闲回收
// 作者：蘅芜君
using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NModbus;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 通信连接池管理器
/// 管理 Modbus 和 TCP 连接的复用、心跳检测。
/// </summary>
public class ConnectionPoolManager : IDisposable
{
    private readonly ILogger<ConnectionPoolManager> _logger;
    private readonly ConcurrentDictionary<string, PooledConnection> _connections = new();
    private readonly Timer _heartbeatTimer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectGates = new();
    private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private readonly IModbusFactory _modbusFactory = new ModbusFactory();
    private int _disposeState;

    public ConnectionPoolManager(ILogger<ConnectionPoolManager> logger)
    {
        _logger = logger;
        _heartbeatTimer = new Timer(CheckConnectionHealth, null, _heartbeatInterval, _heartbeatInterval);
    }

    /// <summary>
    /// 获取或创建Modbus连接
    /// </summary>
    public async Task<IModbusMaster> GetOrCreateModbusConnectionAsync(
        string ipAddress,
        int port,
        byte slaveId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var key = $"modbus:{ipAddress}:{port}:{slaveId}";
        var connectGate = _connectGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (TryGetReusableConnectionUnsafe(key, ConnectionType.Modbus, out var existingConnection) &&
                existingConnection.Connection is IModbusMaster existingMaster)
            {
                _logger.LogDebug("[ConnectionPool] 复用Modbus连接: {Key}", key);
                existingConnection.LastUsedTime = DateTime.UtcNow;
                return existingMaster;
            }
        }
        finally
        {
            _gate.Release();
        }

        await connectGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            await _gate.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();

                if (TryGetReusableConnectionUnsafe(key, ConnectionType.Modbus, out var existingConnection) &&
                    existingConnection.Connection is IModbusMaster existingMaster)
                {
                    _logger.LogDebug("[ConnectionPool] 复用Modbus连接: {Key}", key);
                    existingConnection.LastUsedTime = DateTime.UtcNow;
                    return existingMaster;
                }

                RemoveConnectionUnsafe(key);
            }
            finally
            {
                _gate.Release();
            }

            _logger.LogInformation("[ConnectionPool] Creating new Modbus connection: {Key}", key);
            TcpClient? client = null;
            PooledConnection? pooledConnection = null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                client = await CreateConnectedTcpClientAsync(ipAddress, port, cts.Token);
                var master = _modbusFactory.CreateMaster(client);
                pooledConnection = new PooledConnection
                {
                    Key = key,
                    Connection = master,
                    Client = client,
                    Type = ConnectionType.Modbus,
                    CreatedTime = DateTime.UtcNow,
                    LastUsedTime = DateTime.UtcNow,
                    IsValid = true
                };

                await _gate.WaitAsync(cancellationToken);
                try
                {
                    ThrowIfDisposed();

                    if (TryGetReusableConnectionUnsafe(key, ConnectionType.Modbus, out var currentConnection) &&
                        currentConnection.Connection is IModbusMaster currentMaster)
                    {
                        pooledConnection.Dispose();
                        pooledConnection = null;
                        return currentMaster;
                    }

                    var publishedConnection = pooledConnection
                        ?? throw new InvalidOperationException("Modbus pooled connection was not created.");
                    _connections[key] = publishedConnection;
                    pooledConnection = null;
                    return master;
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch
            {
                pooledConnection?.Dispose();
                client?.Dispose();
                throw;
            }
        }
        finally
        {
            connectGate.Release();
        }
    }

    /// <summary>
    /// 获取或创建TCP连接租约。调用方应释放租约，而不是直接释放底层 NetworkStream。
    /// </summary>
    public async Task<PooledTcpConnectionLease> GetOrCreateTcpConnectionAsync(
        string ipAddress,
        int port,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var key = $"tcp:{ipAddress}:{port}";
        var connectGate = _connectGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (TryGetReusableConnectionUnsafe(key, ConnectionType.Tcp, out var existingConnection) &&
                existingConnection.Connection is NetworkStream existingStream)
            {
                _logger.LogDebug("[ConnectionPool] 复用TCP连接: {Key}", key);
                existingConnection.LastUsedTime = DateTime.UtcNow;
                existingConnection.LeaseCount++;
                return new PooledTcpConnectionLease(existingStream, () => ReleaseTcpLease(key, existingConnection));
            }
        }
        finally
        {
            _gate.Release();
        }

        await connectGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            await _gate.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();

                if (TryGetReusableConnectionUnsafe(key, ConnectionType.Tcp, out var existingConnection) &&
                    existingConnection.Connection is NetworkStream existingStream)
                {
                    _logger.LogDebug("[ConnectionPool] 复用TCP连接: {Key}", key);
                    existingConnection.LastUsedTime = DateTime.UtcNow;
                    existingConnection.LeaseCount++;
                    return new PooledTcpConnectionLease(existingStream, () => ReleaseTcpLease(key, existingConnection));
                }

                RemoveConnectionUnsafe(key);
            }
            finally
            {
                _gate.Release();
            }

            _logger.LogInformation("[ConnectionPool] Creating new TCP connection: {Key}", key);
            TcpClient? client = null;
            PooledConnection? pooledConnection = null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                client = await CreateConnectedTcpClientAsync(ipAddress, port, cts.Token);
                var stream = client.GetStream();
                pooledConnection = new PooledConnection
                {
                    Key = key,
                    Connection = stream,
                    Client = client,
                    Type = ConnectionType.Tcp,
                    CreatedTime = DateTime.UtcNow,
                    LastUsedTime = DateTime.UtcNow,
                    IsValid = true,
                    LeaseCount = 1
                };

                await _gate.WaitAsync(cancellationToken);
                try
                {
                    ThrowIfDisposed();

                    if (TryGetReusableConnectionUnsafe(key, ConnectionType.Tcp, out var currentConnection) &&
                        currentConnection.Connection is NetworkStream currentStream)
                    {
                        currentConnection.LastUsedTime = DateTime.UtcNow;
                        currentConnection.LeaseCount++;
                        pooledConnection.Dispose();
                        pooledConnection = null;
                        return new PooledTcpConnectionLease(currentStream, () => ReleaseTcpLease(key, currentConnection));
                    }

                    var leaseConnection = pooledConnection
                        ?? throw new InvalidOperationException("TCP pooled connection was not created.");
                    _connections[key] = leaseConnection;
                    pooledConnection = null;
                    return new PooledTcpConnectionLease(stream, () => ReleaseTcpLease(key, leaseConnection));
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch
            {
                pooledConnection?.Dispose();
                client?.Dispose();
                throw;
            }
        }
        finally
        {
            connectGate.Release();
        }
    }

    protected virtual async Task<TcpClient> CreateConnectedTcpClientAsync(
        string ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(ipAddress, port, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 释放指定连接
    /// </summary>
    public void ReleaseConnection(string key)
    {
        _gate.Wait();
        try
        {
            RemoveConnectionUnsafe(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 检查连接健康状态
    /// </summary>
    private void CheckConnectionHealth(object? state)
    {
        if (IsDisposed || !_gate.Wait(0))
        {
            return;
        }

        try
        {
            if (IsDisposed)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _connections)
            {
                var connection = kvp.Value;
                if (connection.Type == ConnectionType.Tcp && connection.LeaseCount > 0)
                {
                    continue;
                }

                if (now - connection.LastUsedTime > _maxIdleTime)
                {
                    _logger.LogDebug("[ConnectionPool] 连接超时未使用，准备释放: {Key}", kvp.Key);
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                if (!IsConnectionAlive(connection))
                {
                    _logger.LogWarning("[ConnectionPool] 连接已断开，标记为无效: {Key}", kvp.Key);
                    connection.IsValid = false;
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                RemoveConnectionUnsafe(key);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogInformation("[ConnectionPool] Cleaned up {Count} invalid connections.", keysToRemove.Count);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 检查连接是否存活
    /// </summary>
    private bool IsConnectionAlive(PooledConnection connection)
    {
        try
        {
            var socket = connection.Client?.Client;
            if (socket == null || !socket.Connected)
            {
                return false;
            }

            if (socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取连接池统计信息
    /// </summary>
    public ConnectionPoolStats GetStats()
    {
        return new ConnectionPoolStats
        {
            TotalConnections = _connections.Count,
            ModbusConnections = _connections.Count(c => c.Value.Type == ConnectionType.Modbus),
            TcpConnections = _connections.Count(c => c.Value.Type == ConnectionType.Tcp),
            ActiveConnections = _connections.Count(c => c.Value.IsValid)
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _heartbeatTimer.Dispose();

        _gate.Wait();
        try
        {
            foreach (var key in _connections.Keys.ToList())
            {
                RemoveConnectionUnsafe(key, forceDispose: true);
            }

            _connections.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void ReleaseTcpLease(string key, PooledConnection connection)
    {
        _gate.Wait();
        try
        {
            if (connection.LeaseCount > 0)
            {
                connection.LeaseCount--;
            }

            connection.LastUsedTime = DateTime.UtcNow;

            var isCurrentConnection = _connections.TryGetValue(key, out var currentConnection) &&
                ReferenceEquals(currentConnection, connection);
            if (connection.LeaseCount == 0 && (connection.PendingDispose || !isCurrentConnection || IsDisposed))
            {
                connection.Dispose();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetReusableConnectionUnsafe(string key, ConnectionType expectedType, out PooledConnection connection)
    {
        if (_connections.TryGetValue(key, out connection!))
        {
            if (connection.Type == expectedType && connection.IsValid && !connection.PendingDispose)
            {
                return true;
            }
        }

        connection = null!;
        return false;
    }

    private void RemoveConnectionUnsafe(string key, bool forceDispose = false)
    {
        if (_connections.TryRemove(key, out var connection))
        {
            _logger.LogInformation("[ConnectionPool] 释放连接: {Key}", key);
            RetireConnectionUnsafe(connection, forceDispose);
        }
    }

    private static void RetireConnectionUnsafe(PooledConnection connection, bool forceDispose)
    {
        connection.IsValid = false;
        if (!forceDispose && connection.Type == ConnectionType.Tcp && connection.LeaseCount > 0)
        {
            connection.PendingDispose = true;
            return;
        }

        connection.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}

/// <summary>
/// TCP 连接租约。底层 stream 由连接池持有，调用方释放租约即可归还连接。
/// </summary>
public sealed class PooledTcpConnectionLease : IDisposable
{
    private readonly Action _release;
    private int _disposeState;

    internal PooledTcpConnectionLease(NetworkStream stream, Action release)
    {
        Stream = stream;
        _release = release;
    }

    public NetworkStream Stream { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _release();
    }
}

/// <summary>
/// 连接池中的连接包装
/// </summary>
public class PooledConnection : IDisposable
{
    public string Key { get; set; } = string.Empty;
    public object Connection { get; set; } = null!;
    public TcpClient Client { get; set; } = null!;
    public ConnectionType Type { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastUsedTime { get; set; }
    public bool IsValid { get; set; }
    public int LeaseCount { get; set; }
    public bool PendingDispose { get; set; }

    public void Dispose()
    {
        try
        {
            if (Connection is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Client?.Dispose();
        }
        catch
        {
        }
    }
}

/// <summary>
/// 连接类型
/// </summary>
public enum ConnectionType
{
    Modbus,
    Tcp
}

/// <summary>
/// 连接池统计信息
/// </summary>
public class ConnectionPoolStats
{
    public int TotalConnections { get; set; }
    public int ModbusConnections { get; set; }
    public int TcpConnections { get; set; }
    public int ActiveConnections { get; set; }
}
