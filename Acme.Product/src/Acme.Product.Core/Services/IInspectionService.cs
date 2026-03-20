// IInspectionService.cs
// 获取统计信息
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Core.Services;

/// <summary>
/// 检测执行服务接口
/// </summary>
public interface IInspectionService
{
    /// <summary>
    /// 执行单次检测
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="imageData">图像数据</param>
    /// <returns>检测结果</returns>
    Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData);

    /// <summary>
    /// 执行单次检测（使用前端提供的流程数据）
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="imageData">图像数据</param>
    /// <param name="flow">流程数据（含前端编辑过的参数）</param>
    /// <returns>检测结果</returns>
    Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData, OperatorFlow? flow);

    /// <summary>
    /// 执行单次检测（使用相机采集）
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="cameraId">相机ID</param>
    /// <returns>检测结果</returns>
    Task<InspectionResult> ExecuteSingleAsync(Guid projectId, string cameraId);

    /// <summary>
    /// 开始实时检测（相机驱动模式 - 兼容旧模式）
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="cameraId">相机ID（可选，为空则使用流程内图像采集算子）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartRealtimeInspectionAsync(
        Guid projectId,
        string? cameraId,
        CancellationToken cancellationToken,
        Action<InspectionResult>? onResultReady = null);

    /// <summary>
    /// 开始实时检测（流程驱动模式）
    /// 流程将循环执行，直到调用 StopRealtimeInspectionAsync
    /// 适用于PLC触发等工业场景
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="flow">流程数据</param>
    /// <param name="cameraId">相机ID（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartRealtimeInspectionFlowAsync(
        Guid projectId,
        OperatorFlow flow,
        string? cameraId,
        CancellationToken cancellationToken,
        Action<InspectionResult>? onResultReady = null);

    /// <summary>
    /// 停止实时检测
    /// </summary>
    /// <param name="projectId">工程ID</param>
    Task StopRealtimeInspectionAsync(Guid projectId);

    /// <summary>
    /// 获取检测历史
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    Task<InspectionHistoryPage> GetInspectionHistoryAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? status = null,
        string? defectType = null,
        int pageIndex = 0,
        int pageSize = 20);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <param name="projectId">工程ID</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    Task<InspectionStatistics> GetStatisticsAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? status = null,
        string? defectType = null);
}
