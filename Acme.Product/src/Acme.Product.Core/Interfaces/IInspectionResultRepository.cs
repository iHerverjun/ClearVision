// IInspectionResultRepository.cs
// 检测统计信息
// 作者：蘅芜君

using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 检测结果仓储接口
/// </summary>
public interface IInspectionResultRepository : IRepository<InspectionResult>
{
    /// <summary>
    /// 根据工程ID获取结果列表
    /// </summary>
    Task<IEnumerable<InspectionResult>> GetByProjectIdAsync(Guid projectId, int pageIndex = 0, int pageSize = 20);

    /// <summary>
    /// 获取统一分页语义的检测历史记录。
    /// </summary>
    Task<InspectionHistoryPage> GetHistoryPageAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? status = null,
        string? defectType = null,
        int pageIndex = 0,
        int pageSize = 20);

    /// <summary>
    /// 根据时间范围获取结果
    /// </summary>
    Task<IEnumerable<InspectionResult>> GetByTimeRangeAsync(
        Guid projectId,
        DateTime startTime,
        DateTime endTime,
        string? status = null,
        string? defectType = null);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<InspectionStatistics> GetStatisticsAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? status = null,
        string? defectType = null);

    /// <summary>
    /// 获取缺陷分布统计
    /// </summary>
    Task<Dictionary<Enums.DefectType, int>> GetDefectDistributionAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? status = null,
        string? defectType = null);
}

/// <summary>
/// 检测统计信息
/// </summary>
public class InspectionStatistics
{
    public int TotalCount { get; set; }
    public int OKCount { get; set; }
    public int NGCount { get; set; }
    public int ErrorCount { get; set; }
    public double OKRate { get; set; }
    public double AverageProcessingTimeMs { get; set; }
}

public class InspectionHistoryPage
{
    public IReadOnlyList<InspectionResult> Items { get; set; } = Array.Empty<InspectionResult>();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
