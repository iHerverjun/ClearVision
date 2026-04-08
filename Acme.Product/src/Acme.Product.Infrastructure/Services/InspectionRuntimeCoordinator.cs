// InspectionRuntimeCoordinator.cs
// 实时检测运行时协调器实现
// 职责：线程安全的会话状态管理
// 作者：架构修复方案 v2

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 实时检测运行时协调器实现
/// 线程安全：使用 ConcurrentDictionary + 单一状态锁串行化状态迁移和清理。
/// </summary>
public class InspectionRuntimeCoordinator : IInspectionRuntimeCoordinator, IDisposable
{
    private readonly ILogger<InspectionRuntimeCoordinator> _logger;

    // 会话状态存储
    private readonly ConcurrentDictionary<Guid, RuntimeSession> _sessions = new();

    // 取消令牌源存储（用于 Worker 取消）
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _ctsMap = new();

    // 单一状态锁，避免 project lock 字典长期累积并让生命周期边界更直观。
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly ConcurrentDictionary<(Guid ProjectId, Guid SessionId), Task> _cleanupTasks = new();

    // 关机标志
    private volatile bool _isShuttingDown = false;

    public InspectionRuntimeCoordinator(ILogger<InspectionRuntimeCoordinator> logger)
    {
        _logger = logger;
    }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public Task<StartResult> TryStartAsync(Guid projectId, Guid sessionId, CancellationToken ct)
    {
        if (_isShuttingDown)
        {
            _logger.LogWarning("[Coordinator] 拒绝启动请求，正在关机中: {ProjectId}", projectId);
            return Task.FromResult(StartResult.ShutdownInProgress);
        }

        return StartAsyncInternal(projectId, sessionId, ct);
    }

    private async Task<StartResult> StartAsyncInternal(
        Guid projectId,
        Guid sessionId,
        CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (_isShuttingDown)
            {
                return StartResult.ShutdownInProgress;
            }

            // 双重检查：避免锁等待期间状态变化
            if (_sessions.TryGetValue(projectId, out var existing))
            {
                if (existing.Status == RuntimeStatus.Running || existing.Status == RuntimeStatus.Starting)
                {
                    _logger.LogWarning("[Coordinator] 项目 {ProjectId} 已在运行中: {SessionId}", 
                        projectId, existing.SessionId);
                    return StartResult.AlreadyRunning;
                }

                // 清理旧会话状态
                CleanupSessionCore(projectId, existing.SessionId);
            }

            // 创建新的取消令牌源（与 HTTP 请求 token 无关）
            var cts = new CancellationTokenSource();
            _ctsMap[projectId] = cts;

            // 创建新会话
            var session = new RuntimeSession
            {
                ProjectId = projectId,
                SessionId = sessionId,
                Status = RuntimeStatus.Starting,
                StartedAt = DateTime.UtcNow,
                CancellationTokenSource = cts
            };
            
            _sessions[projectId] = session;
            
            _logger.LogInformation("[Coordinator] 会话已启动: {ProjectId}, Session: {SessionId}", 
                projectId, sessionId);
            
            RaiseStateChanged(projectId, sessionId, RuntimeStatus.Starting, RuntimeStatus.Stopped);
            
            return StartResult.Success;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<bool> TryStopAsync(Guid projectId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
        {
            _logger.LogWarning("[Coordinator] 尝试停止不存在的会话: {ProjectId}", projectId);
            return false;
        }

        CancellationTokenSource? workerCts = null;
        await _stateLock.WaitAsync(ct);
        try
        {
            // 再次检查状态
            if (!_sessions.TryGetValue(projectId, out session))
                return false;

            if (session.Status != RuntimeStatus.Running && session.Status != RuntimeStatus.Starting)
            {
                _logger.LogWarning("[Coordinator] 会话不在运行状态: {ProjectId}, Status: {Status}", 
                    projectId, session.Status);
                return false;
            }

            // 更新状态
            var oldStatus = session.Status;
            session.Status = RuntimeStatus.Stopping;
            RaiseStateChanged(projectId, session.SessionId, RuntimeStatus.Stopping, oldStatus);
            workerCts = session.CancellationTokenSource;
        }
        finally
        {
            _stateLock.Release();
        }

        // 触发取消
        if (workerCts != null)
        {
            try
            {
                await workerCts.CancelAsync();
                _logger.LogInformation("[Coordinator] 已发送取消信号: {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Coordinator] 取消令牌触发失败: {ProjectId}", projectId);
            }
        }

        return true;
    }

    public RuntimeState? GetState(Guid projectId)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
            return null;

        return new RuntimeState
        {
            ProjectId = session.ProjectId,
            SessionId = session.SessionId,
            Status = session.Status,
            StartedAt = session.StartedAt,
            StoppedAt = session.StoppedAt,
            ErrorMessage = session.ErrorMessage
        };
    }

    public CancellationToken GetCancellationToken(Guid projectId)
    {
        return _ctsMap.TryGetValue(projectId, out var cts) 
            ? cts.Token 
            : CancellationToken.None;
    }

    public void MarkAsFaulted(Guid projectId, Guid sessionId, string errorMessage)
    {
        RuntimeSession? session;
        RuntimeStatus oldStatus;
        _stateLock.Wait();
        try
        {
            if (!TryGetMatchingSessionUnsafe(projectId, sessionId, out session))
            {
                return;
            }

            oldStatus = session.Status;
            if (oldStatus == RuntimeStatus.Faulted)
            {
                return;
            }

            session.Status = RuntimeStatus.Faulted;
            session.ErrorMessage = errorMessage;
            session.StoppedAt = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }

        _logger.LogError("[Coordinator] 会话故障: {ProjectId}, Error: {Error}", 
            projectId, errorMessage);

        RaiseStateChanged(projectId, session!.SessionId, RuntimeStatus.Faulted, oldStatus, errorMessage);
        ScheduleCleanup(projectId, sessionId);
    }

    public void MarkAsStopped(Guid projectId, Guid sessionId)
    {
        RuntimeSession? session;
        RuntimeStatus oldStatus;
        _stateLock.Wait();
        try
        {
            if (!TryGetMatchingSessionUnsafe(projectId, sessionId, out session))
            {
                return;
            }

            oldStatus = session.Status;
            if (oldStatus is RuntimeStatus.Stopped or RuntimeStatus.Faulted)
            {
                return;
            }

            session.Status = RuntimeStatus.Stopped;
            session.StoppedAt = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }

        _logger.LogInformation("[Coordinator] 会话已停止: {ProjectId}", projectId);

        RaiseStateChanged(projectId, session!.SessionId, RuntimeStatus.Stopped, oldStatus);
        ScheduleCleanup(projectId, sessionId);
    }

    public void UpdateSessionStatus(Guid projectId, Guid sessionId, RuntimeStatus status)
    {
        RuntimeSession? session;
        RuntimeStatus oldStatus;
        _stateLock.Wait();
        try
        {
            if (!TryGetMatchingSessionUnsafe(projectId, sessionId, out session))
            {
                return;
            }

            oldStatus = session.Status;
            if (oldStatus == status)
            {
                return;
            }

            session.Status = status;
        }
        finally
        {
            _stateLock.Release();
        }
        _logger.LogInformation("[Coordinator] 会话状态更新: {ProjectId}, {OldStatus} -> {NewStatus}", 
            projectId, oldStatus, status);

        RaiseStateChanged(projectId, session!.SessionId, status, oldStatus);
    }

    public IEnumerable<RuntimeState> GetActiveSessions()
    {
        return _sessions
            .Where(kvp => kvp.Value.Status == RuntimeStatus.Running || kvp.Value.Status == RuntimeStatus.Starting)
            .Select(kvp => new RuntimeState
            {
                ProjectId = kvp.Value.ProjectId,
                SessionId = kvp.Value.SessionId,
                Status = kvp.Value.Status,
                StartedAt = kvp.Value.StartedAt
            })
            .ToList();
    }

    /// <summary>
    /// 准备关机：阻止新任务，返回活跃会话列表
    /// </summary>
    public IEnumerable<RuntimeSession> PrepareShutdown()
    {
        _isShuttingDown = true;
        return _sessions.Values.Where(s => s.Status == RuntimeStatus.Running || s.Status == RuntimeStatus.Starting);
    }

    private void RaiseStateChanged(Guid projectId, Guid sessionId, RuntimeStatus newStatus, RuntimeStatus oldStatus, string? errorMessage = null)
    {
        StateChanged?.Invoke(this, new StateChangedEventArgs
        {
            ProjectId = projectId,
            SessionId = sessionId,
            NewStatus = newStatus,
            OldStatus = oldStatus,
            ErrorMessage = errorMessage
        });
    }

    private void ScheduleCleanup(Guid projectId, Guid sessionId)
    {
        var cleanupKey = (projectId, sessionId);
        if (!_cleanupTasks.TryAdd(cleanupKey, Task.CompletedTask))
        {
            return;
        }

        var cleanupTask = CleanupSessionAsync(projectId, sessionId);
        _cleanupTasks.TryUpdate(cleanupKey, cleanupTask, Task.CompletedTask);
    }

    private async Task CleanupSessionAsync(Guid projectId, Guid sessionId)
    {
        try
        {
            // 延迟清理，确保 Worker 有机会处理取消
            await Task.Delay(100);
            await _stateLock.WaitAsync();
            try
            {
                CleanupSessionCore(projectId, sessionId);
            }
            finally
            {
                _stateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coordinator] CleanupSessionAsync 失败: {ProjectId}", projectId);
        }
        finally
        {
            _cleanupTasks.TryRemove((projectId, sessionId), out _);
        }
    }

    private bool TryGetMatchingSessionUnsafe(
        Guid projectId,
        Guid expectedSessionId,
        [NotNullWhen(true)] out RuntimeSession? session)
    {
        if (!_sessions.TryGetValue(projectId, out session))
        {
            _logger.LogWarning("[Coordinator] 尝试操作不存在的会话: {ProjectId}, Session: {SessionId}",
                projectId, expectedSessionId);
            return false;
        }

        if (session.SessionId != expectedSessionId)
        {
            _logger.LogDebug("[Coordinator] 忽略过期会话操作: {ProjectId}, ExpectedSession: {ExpectedSessionId}, CurrentSession: {CurrentSessionId}",
                projectId, expectedSessionId, session.SessionId);
            session = null;
            return false;
        }

        return true;
    }

    private void CleanupSessionCore(Guid projectId, Guid expectedSessionId)
    {
        if (!TryGetMatchingSessionUnsafe(projectId, expectedSessionId, out var session))
        {
            return;
        }

        if (_ctsMap.TryGetValue(projectId, out var currentCts) &&
            ReferenceEquals(currentCts, session!.CancellationTokenSource) &&
            _ctsMap.TryRemove(projectId, out var removedCts))
        {
            try
            {
                removedCts.Cancel();
                removedCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Coordinator] 清理 CTS 时异常: {ProjectId}, Session: {SessionId}",
                    projectId, expectedSessionId);
            }
        }

        _sessions.TryRemove(projectId, out _);
    }

    public void Dispose()
    {
        _isShuttingDown = true;

        var pendingCleanupTasks = _cleanupTasks.Values.ToArray();

        _stateLock.Wait();
        try
        {
            foreach (var cts in _ctsMap.Values)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch
                {
                }
            }

            _ctsMap.Clear();
            _sessions.Clear();
        }
        finally
        {
            _stateLock.Release();
        }

        try
        {
            Task.WhenAll(pendingCleanupTasks).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Coordinator] 等待清理任务结束时异常");
        }

    }
}

/// <summary>
/// 内部会话状态（包含 CTS 引用）
/// </summary>
public class RuntimeSession
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required RuntimeStatus Status { get; set; }
    public required DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
