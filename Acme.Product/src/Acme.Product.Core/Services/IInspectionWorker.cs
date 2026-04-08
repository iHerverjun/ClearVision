// IInspectionWorker.cs
// 实时检测工作器接口
// 职责：定义工作器契约，供 Application 层使用
// 作者：架构修复方案 v2

namespace Acme.Product.Core.Services;

/// <summary>
/// 实时检测工作器接口
/// </summary>
public interface IInspectionWorker
{
    /// <summary>
    /// 尝试启动运行
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="flow">流程定义</param>
    /// <param name="cameraId">相机ID（可选）</param>
    /// <returns>是否成功启动</returns>
    Task<bool> TryStartRunAsync(Guid projectId, Guid sessionId, Entities.OperatorFlow flow, string? cameraId);

    /// <summary>
    /// 等待后台任务真正退出并完成清理。
    /// </summary>
    Task<bool> WaitForRunExitAsync(Guid projectId, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 等待指定会话对应的后台任务真正退出并完成清理。
    /// 如果项目已切换到其他会话，则视为该会话已退出。
    /// </summary>
    Task<bool> WaitForRunExitAsync(Guid projectId, Guid sessionId, TimeSpan timeout, CancellationToken cancellationToken = default);
}
