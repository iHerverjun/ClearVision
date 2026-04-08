// IInspectionRuntimeCoordinator.cs
// 实时检测运行时协调器接口
// 职责：管理实时检测会话状态（进程级单例）
// 作者：架构修复方案 v2

namespace Acme.Product.Core.Services;

/// <summary>
/// 实时检测运行时协调器
/// 职责：管理实时检测会话状态，不执行具体业务逻辑
/// 生命周期：Singleton（进程级单例）
/// </summary>
public interface IInspectionRuntimeCoordinator
{
    /// <summary>
    /// 尝试启动实时检测会话
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="sessionId">会话ID（由调用方生成）</param>
    /// <param name="ct">取消令牌（仅用于等待锁，不影响已启动的会话）</param>
    /// <returns>启动结果</returns>
    Task<StartResult> TryStartAsync(Guid projectId, Guid sessionId, CancellationToken ct);

    /// <summary>
    /// 尝试停止实时检测会话
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否成功停止</returns>
    Task<bool> TryStopAsync(Guid projectId, CancellationToken ct);

    /// <summary>
    /// 获取会话状态
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>会话状态，如果不存在返回 null</returns>
    RuntimeState? GetState(Guid projectId);

    /// <summary>
    /// 获取会话的取消令牌（用于 Worker 内部取消）
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>取消令牌，如果不存在返回 CancellationToken.None</returns>
    CancellationToken GetCancellationToken(Guid projectId);

    /// <summary>
    /// 标记会话为故障状态
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="errorMessage">错误信息</param>
    void MarkAsFaulted(Guid projectId, Guid sessionId, string errorMessage);

    /// <summary>
    /// 标记会话为已停止
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="sessionId">会话ID</param>
    void MarkAsStopped(Guid projectId, Guid sessionId);

    /// <summary>
    /// 更新会话状态
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="status">新状态</param>
    void UpdateSessionStatus(Guid projectId, Guid sessionId, RuntimeStatus status);

    /// <summary>
    /// 状态变更事件
    /// </summary>
    event EventHandler<StateChangedEventArgs> StateChanged;

    /// <summary>
    /// 获取所有活跃会话
    /// </summary>
    IEnumerable<RuntimeState> GetActiveSessions();
}

/// <summary>
/// 启动结果
/// </summary>
public enum StartResult
{
    Success,
    AlreadyRunning,
    ShutdownInProgress
}

/// <summary>
/// 运行时状态
/// </summary>
public class RuntimeState
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required RuntimeStatus Status { get; set; }
    public required DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 运行时状态枚举
/// </summary>
public enum RuntimeStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Faulted
}

/// <summary>
/// 状态变更事件参数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required RuntimeStatus NewStatus { get; init; }
    public required RuntimeStatus OldStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
