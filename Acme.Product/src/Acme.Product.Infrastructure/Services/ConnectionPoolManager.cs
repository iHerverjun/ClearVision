// ConnectionPoolManager.cs
// 连接池统计信息
// 作者：蘅芜君

using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NModbus;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 通信连接池管理器
/// 管理Modbus和TCP连接的复用、心跳检测
/// </summary>
public class ConnectionPoolManager : IDisposable
{
    private readonly ILogger<ConnectionPoolManager> _logger;
    private readonly ConcurrentDictionary<string, PooledConnection> _connections = new();
    private readonly Timer _heartbeatTimer;
    private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private readonly IModbusFactory _modbusFactory = new ModbusFactory();
    private bool _disposed;

    public ConnectionPoolManager(ILogger<ConnectionPoolManager> logger)
    {
        _logger = logger;
        // 启动心跳检测定时器
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
        var key = $"modbus:{ipAddress}:{port}:{slaveId}";
        
        if (_connections.TryGetValue(key, out var existingConnection) && existingConnection.IsValid)
        {
            _logger.LogDebug("[ConnectionPool] 复用Modbus连接: {Key}", key);
            existingConnection.LastUsedTime = DateTime.UtcNow;
            return (IModbusMaster)existingConnection.Connection;
        }

        // 创建新连接
        _logger.LogInformation("[ConnectionPool] 创建新Modbus连接: {Key}", key);
        var client = new TcpClient();
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            await client.ConnectAsync(ipAddress, port, cts.Token);
            var master = _modbusFactory.CreateMaster(client);

            var pooledConnection = new PooledConnection
            {
                Key = key,
                Connection = master,
                Client = client,
                Type = ConnectionType.Modbus,
                CreatedTime = DateTime.UtcNow,
                LastUsedTime = DateTime.UtcNow,
                IsValid = true
            };

            // 如果已存在旧连接，先关闭
            if (_connections.TryRemove(key, out var oldConnection))
            {
                oldConnection.Dispose();
            }

            _connections[key] = pooledConnection;
            return master;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 获取或创建TCP连接
    /// </summary>
    public async Task<NetworkStream> GetOrCreateTcpConnectionAsync(
        string ipAddress, 
        int port,
        CancellationToken cancellationToken = default)
    {
        var key = $"tcp:{ipAddress}:{port}";
        
        if (_connections.TryGetValue(key, out var existingConnection) && existingConnection.IsValid)
        {
            _logger.LogDebug("[ConnectionPool] 复用TCP连接: {Key}", key);
            existingConnection.LastUsedTime = DateTime.UtcNow;
            return (NetworkStream)existingConnection.Connection;
        }

        // 创建新连接
        _logger.LogInformation("[ConnectionPool] 创建新TCP连接: {Key}", key);
        var client = new TcpClient();
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            await client.ConnectAsync(ipAddress, port, cts.Token);
            var stream = client.GetStream();

            var pooledConnection = new PooledConnection
            {
                Key = key,
                Connection = stream,
                Client = client,
                Type = ConnectionType.Tcp,
                CreatedTime = DateTime.UtcNow,
                LastUsedTime = DateTime.UtcNow,
                IsValid = true
            };

            // 如果已存在旧连接，先关闭
            if (_connections.TryRemove(key, out var oldConnection))
            {
                oldConnection.Dispose();
            }

            _connections[key] = pooledConnection;
            return stream;
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
        if (_connections.TryRemove(key, out var connection))
        {
            _logger.LogInformation("[ConnectionPool] 释放连接: {Key}", key);
            connection.Dispose();
        }
    }

    /// <summary>
    /// 检查连接健康状态
    /// </summary>
    private void CheckConnectionHealth(object? state)
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _connections)
        {
            var connection = kvp.Value;
            
            // 检查是否超时未使用
            if (now - connection.LastUsedTime > _maxIdleTime)
            {
                _logger.LogDebug("[ConnectionPool] 连接超时未使用，准备释放: {Key}", kvp.Key);
                keysToRemove.Add(kvp.Key);
                continue;
            }

            // 检查连接是否仍然有效
            if (!IsConnectionAlive(connection))
            {
                _logger.LogWarning("[ConnectionPool] 连接已断开，标记为无效: {Key}", kvp.Key);
                connection.IsValid = false;
                keysToRemove.Add(kvp.Key);
            }
        }

        // 释放无效连接
        foreach (var key in keysToRemove)
        {
            if (_connections.TryRemove(key, out var connection))
            {
                connection.Dispose();
            }
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("[ConnectionPool] 清理了 {Count} 个无效连接", keysToRemove.Count);
        }
    }

    /// <summary>
    /// 检查连接是否存活
    /// </summary>
    private bool IsConnectionAlive(PooledConnection connection)
    {
        try
        {
            if (connection.Client is TcpClient tcpClient)
            {
                return tcpClient.Connected;
            }
            return false;
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
        if (_disposed) return;
        
        _heartbeatTimer?.Dispose();
        
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
        
        _disposed = true;
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
        catch { }
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