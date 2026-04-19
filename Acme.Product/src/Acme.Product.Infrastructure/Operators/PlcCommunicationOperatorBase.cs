// PlcCommunicationOperatorBase.cs
// 创建失败的执行输出
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// PLC通信算子基类
/// </summary>
public abstract class PlcCommunicationOperatorBase : OperatorBase
{
    // ─── 静态连接池 ───────────────────────────────────────────
    private static readonly Dictionary<string, IPlcClient> _connectionPool = new();
    private static readonly SemaphoreSlim _poolLock = new(1, 1);
    private static readonly ConcurrentDictionary<string, RefCountedSemaphore> _connectionKeyLocks = new(StringComparer.Ordinal);

    // ─── 心跳巡检 ─────────────────────────────────────────────
    private static Task? _heartbeatTask;
    private static CancellationTokenSource? _heartbeatCts;
    private static readonly Dictionary<string, bool> _lastKnownState = new();
    private static ILogger? _heartbeatLogger;
    private static bool _heartbeatStarted;

    /// <summary>
    /// 心跳检测间隔（毫秒）
    /// </summary>
    private const int DefaultHeartbeatIntervalMs = 1000;

    /// <summary>
    /// 单次 Ping 超时（毫秒）。超时视为设备忙碌（≈在线）
    /// </summary>
    private const int PingTimeoutMs = 2000;
    private static readonly object _configLock = new();
    private static readonly TimeSpan ConfigRefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions _configJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static CommunicationConfig _cachedCommunicationConfig = new();
    private static DateTime _cachedCommunicationConfigAtUtc = DateTime.MinValue;

    protected PlcCommunicationOperatorBase(ILogger logger) : base(logger)
    {
        // 首次创建算子时自动启动心跳巡检
        if (!_heartbeatStarted)
        {
            _heartbeatLogger = logger;
            StartHeartbeat();
        }
    }

    // ─── 心跳管理 ─────────────────────────────────────────────

    /// <summary>
    /// 启动后台心跳巡检
    /// </summary>
    public static void StartHeartbeat()
    {
        if (_heartbeatStarted)
            return;
        _heartbeatStarted = true;

        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));

        // 注册进程退出时优雅关闭
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopHeartbeat();
    }

    /// <summary>
    /// 停止心跳巡检（应用退出时调用）
    /// </summary>
    public static void StopHeartbeat()
    {
        if (!_heartbeatStarted)
            return;

        _heartbeatCts?.Cancel();
        _heartbeatStarted = false;

        try
        {
            _heartbeatTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // 忽略取消异常
        }
        finally
        {
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;
        }
    }

    /// <summary>
    /// 心跳巡检主循环
    /// </summary>
    private static async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        _heartbeatLogger?.LogInformation("[Heartbeat] 心跳巡检已启动，间隔 {Interval}ms", GetHeartbeatIntervalMs());

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GetHeartbeatIntervalMs(), ct);

                // 获取连接池快照（避免遍历期间集合被修改）
                KeyValuePair<string, IPlcClient>[] snapshot;
                if (!await _poolLock.WaitAsync(200, ct))
                {
                    // 200ms 内拿不到池锁，说明有算子正在建立连接，跳过本轮
                    continue;
                }
                try
                {
                    snapshot = _connectionPool.ToArray();
                }
                finally
                {
                    _poolLock.Release();
                }

                if (snapshot.Length == 0)
                    continue;

                // 逐个检测
                foreach (var (key, client) in snapshot)
                {
                    if (ct.IsCancellationRequested)
                        break;
                    await PingClientAsync(key, client, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break; // 正常关闭
            }
            catch (Exception ex)
            {
                _heartbeatLogger?.LogError(ex, "[Heartbeat] 巡检循环发生意外异常");
            }
        }

        _heartbeatLogger?.LogInformation("[Heartbeat] 心跳巡检已停止");
    }

    /// <summary>
    /// 对单个客户端执行 Ping 检测
    /// </summary>
    private static async Task PingClientAsync(string key, IPlcClient client, CancellationToken ct)
    {
        bool isAlive;

        try
        {
            // 使用短超时，避免阻塞算子的正常读写
            using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pingCts.CancelAfter(PingTimeoutMs);

            isAlive = await client.PingAsync(pingCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Ping 超时 → 设备可能正忙于算子读写，视为在线
            isAlive = true;
        }
        catch
        {
            isAlive = false;
        }

        // ─── 状态变化检测（仅在状态改变时记录日志）──────────
        var hadPreviousState = _lastKnownState.TryGetValue(key, out var wasAlive);

        if (!hadPreviousState)
        {
            // 首次检测到该连接
            _lastKnownState[key] = isAlive;
            return;
        }

        if (wasAlive == isAlive)
            return; // 状态未变化，静默

        // 状态发生变化
        _lastKnownState[key] = isAlive;

        if (isAlive)
        {
            _heartbeatLogger?.LogInformation("[Heartbeat] ✅ 设备恢复在线: {Key}", key);
        }
        else
        {
            _heartbeatLogger?.LogWarning("[Heartbeat] ⚠️ 设备掉线: {Key}，将在下次执行时自动重连", key);

            // 主动断开，确保 IsConnected 状态复位，触发 GetOrCreateConnectionAsync 重连
            try
            {
                await client.DisconnectAsync();
            }
            catch
            {
                // 断开可能失败（连接已丢失），忽略
            }
        }
    }

    // ─── 连接管理 ─────────────────────────────────────────────

    /// <summary>
    /// 获取或创建PLC连接
    /// </summary>
    private sealed class RefCountedSemaphore
    {
        private int _refCount;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public void AddRef()
        {
            Interlocked.Increment(ref _refCount);
        }

        public int ReleaseRef()
        {
            return Interlocked.Decrement(ref _refCount);
        }
    }

    private static RefCountedSemaphore AcquireRefCountedSemaphore(
        ConcurrentDictionary<string, RefCountedSemaphore> dictionary,
        string key)
    {
        while (true)
        {
            var entry = dictionary.GetOrAdd(key, static _ => new RefCountedSemaphore());
            entry.AddRef();

            if (dictionary.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                return entry;
            }

            _ = entry.ReleaseRef();
        }
    }

    private static void ReleaseRefCountedSemaphore(
        ConcurrentDictionary<string, RefCountedSemaphore> dictionary,
        string key,
        RefCountedSemaphore entry)
    {
        if (entry.ReleaseRef() != 0)
        {
            return;
        }

        _ = dictionary.TryRemove(new KeyValuePair<string, RefCountedSemaphore>(key, entry));
    }

    protected async Task<(IPlcClient client, bool isNewConnection)> GetOrCreateConnectionAsync(
        string connectionKey,
        Func<IPlcClient> factory)
    {
        var keyLockEntry = AcquireRefCountedSemaphore(_connectionKeyLocks, connectionKey);
        var keyLockAcquired = false;
        await keyLockEntry.Semaphore.WaitAsync();
        keyLockAcquired = true;
        try
        {
            await _poolLock.WaitAsync();
            try
            {
                if (_connectionPool.TryGetValue(connectionKey, out var existingClient) && existingClient.IsConnected)
                {
                    Logger.LogDebug("[{OperatorType}] 复用现有连接: {Key}", OperatorType, connectionKey);
                    return (existingClient, false);
                }
            }
            finally
            {
                _poolLock.Release();
            }

            // 创建新连接
            Logger.LogInformation("[{OperatorType}] 创建新连接: {Key}", OperatorType, connectionKey);
            var newClient = factory();
            bool connected;
            try
            {
                connected = await newClient.ConnectAsync();
            }
            catch
            {
                newClient.Dispose();
                throw;
            }

            if (!connected)
            {
                newClient.Dispose();
                throw new InvalidOperationException($"无法连接到PLC: {connectionKey}");
            }

            IPlcClient? oldClient = null;
            await _poolLock.WaitAsync();
            try
            {
                // 并发场景下若其他线程已恢复可用连接，则直接复用并回收新客户端
                if (_connectionPool.TryGetValue(connectionKey, out var latestClient) && latestClient.IsConnected)
                {
                    newClient.Dispose();
                    Logger.LogDebug("[{OperatorType}] 复用连接（并发延迟）: {Key}", OperatorType, connectionKey);
                    return (latestClient, false);
                }

                if (_connectionPool.TryGetValue(connectionKey, out oldClient))
                {
                    _connectionPool.Remove(connectionKey);
                }

                _connectionPool[connectionKey] = newClient;

                // 新连接上线，初始化心跳状态
                _lastKnownState[connectionKey] = true;
            }
            finally
            {
                _poolLock.Release();
            }

            oldClient?.Dispose();

            return (newClient, true);
        }
        finally
        {
            if (keyLockAcquired)
            {
                keyLockEntry.Semaphore.Release();
            }

            ReleaseRefCountedSemaphore(_connectionKeyLocks, connectionKey, keyLockEntry);
        }
    }

    // ─── 数据转换工具 ─────────────────────────────────────────

    /// <summary>
    /// 根据数据类型获取长度
    /// </summary>
    protected (string ipAddress, int port, string protocol, string connectionSource) ResolveConnectionSettings(
        string? ipAddress,
        int? port,
        string fallbackProtocol = "",
        bool useGlobalFallback = false)
    {
        var global = GetGlobalCommunicationConfig();
        var normalizedProtocol = CommunicationConfig.NormalizeProtocolKey(fallbackProtocol, global.ActiveProtocol) ?? string.Empty;
        var globalProfile = global.GetProfile(normalizedProtocol);
        var normalizedIp = (ipAddress ?? string.Empty).Trim();
        var requestedPort = port ?? 0;
        var hasOperatorIp = !string.IsNullOrWhiteSpace(normalizedIp);
        var hasOperatorPort = requestedPort > 0;
        var globalIp = (globalProfile.IpAddress ?? string.Empty).Trim();
        var hasGlobalIp = !string.IsNullOrWhiteSpace(globalIp);
        var hasGlobalPort = globalProfile.Port > 0 && globalProfile.Port <= 65535;

        if (hasOperatorPort && (requestedPort < 1 || requestedPort > 65535))
        {
            throw new InvalidOperationException(BuildConnectionConfigErrorMessage(
                code: "PLC_CONNECTION_CONFIG_INVALID_PORT",
                message: "Operator Port must be within 1..65535.",
                protocol: normalizedProtocol,
                useGlobalFallback: useGlobalFallback,
                hasOperatorIp: hasOperatorIp,
                hasOperatorPort: hasOperatorPort,
                hasGlobalIp: hasGlobalIp,
                hasGlobalPort: hasGlobalPort));
        }

        if (!useGlobalFallback)
        {
            if (!hasOperatorIp || !hasOperatorPort)
            {
                throw new InvalidOperationException(BuildConnectionConfigErrorMessage(
                    code: "PLC_CONNECTION_CONFIG_OPERATOR_REQUIRED",
                    message: "Operator IpAddress and Port are required when UseGlobalFallback is false.",
                    protocol: normalizedProtocol,
                    useGlobalFallback: false,
                    hasOperatorIp: hasOperatorIp,
                    hasOperatorPort: hasOperatorPort,
                    hasGlobalIp: hasGlobalIp,
                    hasGlobalPort: hasGlobalPort));
            }

            return (normalizedIp, requestedPort, normalizedProtocol, "OperatorParameters");
        }

        var resolvedIp = hasOperatorIp ? normalizedIp : globalIp;
        var resolvedPort = hasOperatorPort ? requestedPort : globalProfile.Port;
        var usedGlobalFallback = !hasOperatorIp || !hasOperatorPort;

        if (string.IsNullOrWhiteSpace(resolvedIp))
        {
            throw new InvalidOperationException(BuildConnectionConfigErrorMessage(
                code: "PLC_CONNECTION_CONFIG_MISSING_IP",
                message: "PLC IP is not configured in operator parameters and global settings.",
                protocol: normalizedProtocol,
                useGlobalFallback: true,
                hasOperatorIp: hasOperatorIp,
                hasOperatorPort: hasOperatorPort,
                hasGlobalIp: hasGlobalIp,
                hasGlobalPort: hasGlobalPort));
        }

        if (resolvedPort <= 0 || resolvedPort > 65535)
        {
            throw new InvalidOperationException(BuildConnectionConfigErrorMessage(
                code: "PLC_CONNECTION_CONFIG_MISSING_PORT",
                message: "PLC Port is not configured in operator parameters and global settings.",
                protocol: normalizedProtocol,
                useGlobalFallback: true,
                hasOperatorIp: hasOperatorIp,
                hasOperatorPort: hasOperatorPort,
                hasGlobalIp: hasGlobalIp,
                hasGlobalPort: hasGlobalPort));
        }

        if (usedGlobalFallback)
        {
            Logger.LogInformation(
                "[{OperatorType}] Connection fallback applied. Operator IP='{OperatorIp}', Port='{OperatorPort}', Global IP='{GlobalIp}', Port={GlobalPort}.",
                OperatorType,
                ipAddress,
                port,
                globalProfile.IpAddress,
                globalProfile.Port);
        }

        return (resolvedIp, resolvedPort, normalizedProtocol, usedGlobalFallback ? "GlobalFallback" : "OperatorParameters");
    }

    private static string BuildConnectionConfigErrorMessage(
        string code,
        string message,
        string protocol,
        bool useGlobalFallback,
        bool hasOperatorIp,
        bool hasOperatorPort,
        bool hasGlobalIp,
        bool hasGlobalPort)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["Code"] = code,
            ["Message"] = message,
            ["Protocol"] = protocol,
            ["UseGlobalFallback"] = useGlobalFallback,
            ["Details"] = new Dictionary<string, object>
            {
                ["HasOperatorIp"] = hasOperatorIp,
                ["HasOperatorPort"] = hasOperatorPort,
                ["HasGlobalIp"] = hasGlobalIp,
                ["HasGlobalPort"] = hasGlobalPort
            }
        });
    }

    private static int GetHeartbeatIntervalMs()
    {
        var intervalMs = GetGlobalCommunicationConfig().HeartbeatIntervalMs;
        return intervalMs > 0 ? intervalMs : DefaultHeartbeatIntervalMs;
    }

    private static CommunicationConfig GetGlobalCommunicationConfig()
    {
        lock (_configLock)
        {
            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _cachedCommunicationConfigAtUtc < ConfigRefreshInterval)
            {
                _cachedCommunicationConfig.Normalize();
                return _cachedCommunicationConfig;
            }

            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, _configJsonOptions);
                    config?.Normalize();
                    if (config?.Communication != null)
                    {
                        _cachedCommunicationConfig = config.Communication;
                        _cachedCommunicationConfig.Normalize();
                    }
                }
            }
            catch (Exception ex)
            {
                _heartbeatLogger?.LogDebug(ex, "[PLC Config] Failed to load global config, using cached defaults.");
            }

            _cachedCommunicationConfigAtUtc = nowUtc;
            _cachedCommunicationConfig.Normalize();
            return _cachedCommunicationConfig;
        }
    }

    protected ushort GetReadElementCount(string dataType)
    {
        // 统一长度语义：ReadAsync 的 length 表示“元素个数/点数”
        // 算子单值读取固定读取 1 个元素，避免协议层重复按类型大小扩展导致过读。
        return 1;
    }

    /// <summary>
    /// 将字节数组转换为指定类型的值
    /// </summary>
    protected object ConvertBytesToValue(IPlcClient client, byte[] data, string dataType)
    {
        var transform = client.ByteTransform;

        return dataType.ToUpper() switch
        {
            "BIT" or "BOOL" => data[0] != 0,
            "BYTE" => data[0],
            "WORD" or "USHORT" => transform.ToUInt16(data, 0),
            "INT16" or "SHORT" => transform.ToInt16(data, 0),
            "DWORD" or "UINT" => transform.ToUInt32(data, 0),
            "INT32" or "INT" => transform.ToInt32(data, 0),
            "FLOAT" => transform.ToFloat(data, 0),
            "LWORD" or "ULONG" => transform.ToUInt64(data, 0),
            "INT64" or "LONG" => transform.ToInt64(data, 0),
            "DOUBLE" => transform.ToDouble(data, 0),
            "STRING" => System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0'),
            _ => data
        };
    }

    /// <summary>
    /// 将值转换为字节数组
    /// </summary>
    protected byte[] ConvertValueToBytes(IPlcClient client, object value, string dataType)
    {
        var transform = client.ByteTransform;

        return dataType.ToUpper() switch
        {
            "BIT" or "BOOL" => new byte[] { Convert.ToBoolean(value) ? (byte)1 : (byte)0 },
            "BYTE" => new byte[] { Convert.ToByte(value) },
            "WORD" or "USHORT" => transform.GetBytes(Convert.ToUInt16(value)),
            "INT16" or "SHORT" => transform.GetBytes(Convert.ToInt16(value)),
            "DWORD" or "UINT" => transform.GetBytes(Convert.ToUInt32(value)),
            "INT32" or "INT" => transform.GetBytes(Convert.ToInt32(value)),
            "FLOAT" => transform.GetBytes(Convert.ToSingle(value)),
            "LWORD" or "ULONG" => transform.GetBytes(Convert.ToUInt64(value)),
            "INT64" or "LONG" => transform.GetBytes(Convert.ToInt64(value)),
            "DOUBLE" => transform.GetBytes(Convert.ToDouble(value)),
            "STRING" => System.Text.Encoding.ASCII.GetBytes(Convert.ToString(value) ?? ""),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

    /// <summary>
    /// 创建成功的执行输出
    /// </summary>
    protected OperatorExecutionOutput CreateSuccessOutput(object value, string dataType)
    {
        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Value"] = value,
            ["DataType"] = dataType,
            ["Status"] = true,
            ["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        });
    }

    protected static void AttachConnectionAuditInfo(OperatorExecutionOutput output, string connectionSource)
    {
        if (!output.IsSuccess)
        {
            return;
        }

        output.OutputData ??= new Dictionary<string, object>();
        output.OutputData["ConnectionSource"] = connectionSource;
    }

    /// <summary>
    /// 创建失败的执行输出
    /// </summary>
    protected OperatorExecutionOutput CreateFailureOutput(string errorMessage)
    {
        return OperatorExecutionOutput.Failure(errorMessage);
    }
}
