// InspectionRuntimeCoordinator.cs
// 实时检测运行时协调器实现
// 职责：线程安全的会话状态管理
// 作者：架构修复方案 v2

using System.Collections.Concurrent;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 实时检测运行时协调器实现
/// 线程安全：使用 ConcurrentDictionary + 每个 projectId 独立锁
/// </summary>
public class InspectionRuntimeCoordinator : IInspectionRuntimeCoordinator, IDisposable
{
    private readonly ILogger<InspectionRuntimeCoordinator> _logger;
    
    // 会话状态存储
    private readonly ConcurrentDictionary<Guid, RuntimeSession> _sessions = new();
    
    // 取消令牌源存储（用于 Worker 取消）
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _ctsMap = new();
    
    // 每个 projectId 一把锁，避免全局锁竞争
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    
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

        var lockObj = _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        
        return StartAsyncInternal(projectId, sessionId, lockObj, ct);
    }

    private async Task<StartResult> StartAsyncInternal(
        Guid projectId, 
        Guid sessionId, 
        SemaphoreSlim lockObj,
        CancellationToken ct)
    {
        await lockObj.WaitAsync(ct);
        try
        {
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
                await CleanupSessionAsync(projectId);
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
            lockObj.Release();
        }
    }

    public async Task<bool> TryStopAsync(Guid projectId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
        {
            _logger.LogWarning("[Coordinator] 尝试停止不存在的会话: {ProjectId}", projectId);
            return false;
        }

        var lockObj = _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(ct);
        
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

            // 触发取消
            if (_ctsMap.TryGetValue(projectId, out var cts))
            {
                try
                {
                    await cts.CancelAsync();
                    _logger.LogInformation("[Coordinator] 已发送取消信号: {ProjectId}", projectId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Coordinator] 取消令牌触发失败: {ProjectId}", projectId);
                }
            }

            return true;
        }
        finally
        {
            lockObj.Release();
        }
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

    public void MarkAsFaulted(Guid projectId, string errorMessage)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
        {
            _logger.LogWarning("[Coordinator] 尝试标记不存在的会话为故障: {ProjectId}", projectId);
            return;
        }

        var oldStatus = session.Status;
        session.Status = RuntimeStatus.Faulted;
        session.ErrorMessage = errorMessage;
        session.StoppedAt = DateTime.UtcNow;

        _logger.LogError("[Coordinator] 会话故障: {ProjectId}, Error: {Error}", 
            projectId, errorMessage);

        RaiseStateChanged(projectId, session.SessionId, RuntimeStatus.Faulted, oldStatus, errorMessage);
        
        // 清理资源
        _ = CleanupSessionAsync(projectId);
    }

    public void MarkAsStopped(Guid projectId)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
            return;

        var oldStatus = session.Status;
        session.Status = RuntimeStatus.Stopped;
        session.StoppedAt = DateTime.UtcNow;

        _logger.LogInformation("[Coordinator] 会话已停止: {ProjectId}", projectId);

        RaiseStateChanged(projectId, session.SessionId, RuntimeStatus.Stopped, oldStatus);
        
        // 清理资源
        _ = CleanupSessionAsync(projectId);
    }

    public void UpdateSessionStatus(Guid projectId, RuntimeStatus status)
    {
        if (!_sessions.TryGetValue(projectId, out var session))
        {
            _logger.LogWarning("[Coordinator] 尝试更新不存在会话的状态: {ProjectId}", projectId);
            return;
        }

        var oldStatus = session.Status;
        if (oldStatus == status)
            return;

        session.Status = status;
        _logger.LogInformation("[Coordinator] 会话状态更新: {ProjectId}, {OldStatus} -> {NewStatus}", 
            projectId, oldStatus, status);

        RaiseStateChanged(projectId, session.SessionId, status, oldStatus);
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

    private async Task CleanupSessionAsync(Guid projectId)
    {
        // 延迟清理，确保 Worker 有机会处理取消
        await Task.Delay(100);

        if (_ctsMap.TryRemove(projectId, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Coordinator] 清理 CTS 时异常: {ProjectId}", projectId);
            }
        }

        _sessions.TryRemove(projectId, out _);
        
        // 注意：不清理 _locks，避免并发问题
    }

    public void Dispose()
    {
        _isShuttingDown = true;
        
        foreach (var cts in _ctsMap.Values)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { /* ignore */ }
        }
        
        _ctsMap.Clear();
        _sessions.Clear();
        
        foreach (var lockObj in _locks.Values)
        {
            lockObj.Dispose();
        }
        _locks.Clear();
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
