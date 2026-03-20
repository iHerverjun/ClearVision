// InspectionResultRepository.cs
// 检测结果仓储实现
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Infrastructure.Repositories;

/// <summary>
/// 检测结果仓储实现
/// </summary>
public class InspectionResultRepository : RepositoryBase<InspectionResult>, IInspectionResultRepository
{
    public InspectionResultRepository(Data.VisionDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<InspectionResult>> GetByProjectIdAsync(Guid projectId, int pageIndex = 0, int pageSize = 20)
    {
        return await _dbSet
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderByDescending(r => r.InspectionTime)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<InspectionHistoryPage> GetHistoryPageAsync(
        Guid projectId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageIndex = 0,
        int pageSize = 20)
    {
        pageIndex = Math.Max(0, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbSet
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .AsQueryable();

        if (startTime.HasValue)
            query = query.Where(r => r.InspectionTime >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(r => r.InspectionTime <= endTime.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.InspectionTime)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new InspectionHistoryPage
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<InspectionResult>> GetByTimeRangeAsync(Guid projectId, DateTime startTime, DateTime endTime)
    {
        return await _dbSet
            .Where(r => r.ProjectId == projectId &&
                       r.InspectionTime >= startTime &&
                       r.InspectionTime <= endTime &&
                       !r.IsDeleted)
            .OrderByDescending(r => r.InspectionTime)
            .ToListAsync();
    }

    public async Task<InspectionStatistics> GetStatisticsAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _dbSet.Where(r => r.ProjectId == projectId && !r.IsDeleted);

        if (startTime.HasValue)
            query = query.Where(r => r.InspectionTime >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(r => r.InspectionTime <= endTime.Value);

        var results = await query.ToListAsync();

        var totalCount = results.Count;
        var okCount = results.Count(r => r.Status == InspectionStatus.OK);
        var ngCount = results.Count(r => r.Status == InspectionStatus.NG);
        var errorCount = results.Count(r => r.Status == InspectionStatus.Error);
        var avgTime = totalCount > 0 ? results.Average(r => r.ProcessingTimeMs) : 0;

        return new InspectionStatistics
        {
            TotalCount = totalCount,
            OKCount = okCount,
            NGCount = ngCount,
            ErrorCount = errorCount,
            OKRate = totalCount > 0 ? (double)okCount / totalCount : 0,
            AverageProcessingTimeMs = avgTime
        };
    }

    public async Task<Dictionary<DefectType, int>> GetDefectDistributionAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _dbSet
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .AsQueryable();

        if (startTime.HasValue)
            query = query.Where(r => r.InspectionTime >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(r => r.InspectionTime <= endTime.Value);

        // 使用 SelectMany 获取所有缺陷
        var defects = await query
            .SelectMany(r => r.Defects)
            .ToListAsync();

        return defects
            .GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
