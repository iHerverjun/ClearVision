// ResultAnalysisService.cs
// 时间间隔枚举
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Application.Services;

/// <summary>
/// 结果分析服务 - 提供检测数据统计、报表和导出功能
/// </summary>
public interface IResultAnalysisService
{
    /// <summary>
    /// 获取检测统计概览
    /// </summary>
    Task<InspectionStatisticsDto> GetStatisticsAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 获取缺陷类型分布
    /// </summary>
    Task<DefectDistributionDto> GetDefectDistributionAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 获取置信度分布
    /// </summary>
    Task<ConfidenceDistributionDto> GetConfidenceDistributionAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 获取检测趋势（按小时/天/周）
    /// </summary>
    Task<TrendAnalysisDto> GetTrendAnalysisAsync(Guid projectId, TrendInterval interval, DateTime startTime, DateTime endTime);

    /// <summary>
    /// 导出检测结果为CSV
    /// </summary>
    Task<string> ExportToCsvAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 导出检测结果为JSON
    /// </summary>
    Task<string> ExportToJsonAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 生成检测报告
    /// </summary>
    Task<InspectionReportDto> GenerateReportAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 对比两个时间段的数据
    /// </summary>
    Task<ComparisonAnalysisDto> ComparePeriodsAsync(Guid projectId, DateTime period1Start, DateTime period1End, DateTime period2Start, DateTime period2End);

    /// <summary>
    /// 获取缺陷热点图数据
    /// </summary>
    Task<DefectHeatmapDto> GetDefectHeatmapAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null);
}

/// <summary>
/// 结果分析服务实现
/// </summary>
public class ResultAnalysisService : IResultAnalysisService
{
    private readonly IInspectionResultRepository _resultRepository;

    public ResultAnalysisService(IInspectionResultRepository resultRepository)
    {
        _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
    }

    /// <inheritdoc />
    public async Task<InspectionStatisticsDto> GetStatisticsAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var statistics = await _resultRepository.GetStatisticsAsync(projectId, startTime, endTime);
        var distribution = await _resultRepository.GetDefectDistributionAsync(projectId, startTime, endTime);

        return new InspectionStatisticsDto
        {
            ProjectId = projectId,
            StartTime = startTime,
            EndTime = endTime,
            TotalCount = statistics.TotalCount,
            OKCount = statistics.OKCount,
            NGCount = statistics.NGCount,
            ErrorCount = statistics.ErrorCount,
            OKRate = statistics.OKRate,
            NGRate = statistics.TotalCount > 0 ? (double)statistics.NGCount / statistics.TotalCount : 0,
            ErrorRate = statistics.TotalCount > 0 ? (double)statistics.ErrorCount / statistics.TotalCount : 0,
            AverageProcessingTimeMs = statistics.AverageProcessingTimeMs,
            TotalDefects = distribution.Values.Sum()
        };
    }

    /// <inheritdoc />
    public async Task<DefectDistributionDto> GetDefectDistributionAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var distribution = await _resultRepository.GetDefectDistributionAsync(projectId, startTime, endTime);
        var total = distribution.Values.Sum();

        var items = distribution.Select(d => new DefectDistributionItemDto
        {
            DefectType = d.Key.ToString(),
            Count = d.Value,
            Percentage = total > 0 ? (double)d.Value / total * 100 : 0
        }).OrderByDescending(i => i.Count).ToList();

        return new DefectDistributionDto
        {
            ProjectId = projectId,
            StartTime = startTime,
            EndTime = endTime,
            TotalDefects = total,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<ConfidenceDistributionDto> GetConfidenceDistributionAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var results = await _resultRepository.GetByProjectIdAsync(projectId, 0, 1000);
        
        // 按时间筛选
        var filtered = results.Where(r =>
            (!startTime.HasValue || r.InspectionTime >= startTime.Value) &&
            (!endTime.HasValue || r.InspectionTime <= endTime.Value));

        var allDefects = filtered.SelectMany(r => r.Defects).ToList();
        
        // 分桶统计
        var buckets = new Dictionary<string, int>
        {
            { "90-100%", 0 },
            { "80-90%", 0 },
            { "70-80%", 0 },
            { "60-70%", 0 },
            { "50-60%", 0 },
            { "<50%", 0 }
        };

        foreach (var defect in allDefects)
        {
            var score = defect.ConfidenceScore * 100;
            switch (score)
            {
                case >= 90: buckets["90-100%"]++; break;
                case >= 80: buckets["80-90%"]++; break;
                case >= 70: buckets["70-80%"]++; break;
                case >= 60: buckets["60-70%"]++; break;
                case >= 50: buckets["50-60%"]++; break;
                default: buckets["<50%"]++; break;
            }
        }

        var total = allDefects.Count;

        return new ConfidenceDistributionDto
        {
            ProjectId = projectId,
            StartTime = startTime,
            EndTime = endTime,
            TotalDefects = total,
            Buckets = buckets.Select(b => new ConfidenceBucketDto
            {
                Range = b.Key,
                Count = b.Value,
                Percentage = total > 0 ? (double)b.Value / total * 100 : 0
            }).ToList(),
            AverageConfidence = allDefects.Any() ? allDefects.Average(d => d.ConfidenceScore) : 0
        };
    }

    /// <inheritdoc />
    public async Task<TrendAnalysisDto> GetTrendAnalysisAsync(Guid projectId, TrendInterval interval, DateTime startTime, DateTime endTime)
    {
        var results = await _resultRepository.GetByTimeRangeAsync(projectId, startTime, endTime);
        var resultList = results.ToList();

        var dataPoints = new List<TrendDataPointDto>();
        
        if (!resultList.Any())
        {
            return new TrendAnalysisDto
            {
                ProjectId = projectId,
                Interval = interval.ToString(),
                StartTime = startTime,
                EndTime = endTime,
                DataPoints = dataPoints
            };
        }

        // 按时间间隔分组
        var current = startTime;
        while (current < endTime)
        {
            var next = interval switch
            {
                TrendInterval.Hour => current.AddHours(1),
                TrendInterval.Day => current.AddDays(1),
                TrendInterval.Week => current.AddDays(7),
                TrendInterval.Month => current.AddMonths(1),
                _ => current.AddDays(1)
            };

            var periodResults = resultList.Where(r => r.InspectionTime >= current && r.InspectionTime < next).ToList();
            
            dataPoints.Add(new TrendDataPointDto
            {
                Timestamp = current,
                TotalCount = periodResults.Count,
                OKCount = periodResults.Count(r => r.Status == InspectionStatus.OK),
                NGCount = periodResults.Count(r => r.Status == InspectionStatus.NG),
                ErrorCount = periodResults.Count(r => r.Status == InspectionStatus.Error),
                OKRate = periodResults.Any() ? (double)periodResults.Count(r => r.Status == InspectionStatus.OK) / periodResults.Count : 0,
                DefectCount = periodResults.Sum(r => r.Defects.Count),
                AverageProcessingTime = periodResults.Any() ? periodResults.Average(r => r.ProcessingTimeMs) : 0
            });

            current = next;
        }

        return new TrendAnalysisDto
        {
            ProjectId = projectId,
            Interval = interval.ToString(),
            StartTime = startTime,
            EndTime = endTime,
            DataPoints = dataPoints
        };
    }

    /// <inheritdoc />
    public async Task<string> ExportToCsvAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var results = await _resultRepository.GetByTimeRangeAsync(projectId, startTime ?? DateTime.MinValue, endTime ?? DateTime.MaxValue);
        
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("检测ID,工程ID,检测时间,状态,处理时间(ms),置信度,缺陷数量,错误信息");

        foreach (var result in results)
        {
            csv.AppendLine($"{result.Id},{result.ProjectId},{result.InspectionTime:yyyy-MM-dd HH:mm:ss},{result.Status},{result.ProcessingTimeMs},{result.ConfidenceScore:F4},{result.Defects.Count},{result.ErrorMessage}");
            
            foreach (var defect in result.Defects)
            {
                csv.AppendLine($",,,,,,,{defect.Type},{defect.X:F2},{defect.Y:F2},{defect.Width:F2},{defect.Height:F2},{defect.ConfidenceScore:F4},{defect.Description}");
            }
        }

        return csv.ToString();
    }

    /// <inheritdoc />
    public async Task<string> ExportToJsonAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var results = await _resultRepository.GetByTimeRangeAsync(projectId, startTime ?? DateTime.MinValue, endTime ?? DateTime.MaxValue);
        
        var exportData = new InspectionExportDto
        {
            ProjectId = projectId,
            ExportTime = DateTime.UtcNow,
            StartTime = startTime,
            EndTime = endTime,
            Results = results.Select(r => new InspectionResultExportItemDto
            {
                Id = r.Id,
                InspectionTime = r.InspectionTime,
                Status = r.Status.ToString(),
                ProcessingTimeMs = r.ProcessingTimeMs,
                ConfidenceScore = r.ConfidenceScore,
                ErrorMessage = r.ErrorMessage,
                Defects = r.Defects.Select(d => new DefectExportDto
                {
                    Type = d.Type.ToString(),
                    X = d.X,
                    Y = d.Y,
                    Width = d.Width,
                    Height = d.Height,
                    ConfidenceScore = d.ConfidenceScore,
                    Description = d.Description
                }).ToList()
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <inheritdoc />
    public async Task<InspectionReportDto> GenerateReportAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var statistics = await GetStatisticsAsync(projectId, startTime, endTime);
        var defectDistribution = await GetDefectDistributionAsync(projectId, startTime, endTime);
        var confidenceDistribution = await GetConfidenceDistributionAsync(projectId, startTime, endTime);

        // 获取最近24小时的趋势（按小时）
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var hourlyTrend = await GetTrendAnalysisAsync(projectId, TrendInterval.Hour, yesterday > (startTime ?? DateTime.MinValue) ? yesterday : startTime ?? DateTime.MinValue, now);

        return new InspectionReportDto
        {
            ProjectId = projectId,
            GeneratedAt = DateTime.UtcNow,
            Period = new ReportPeriodDto
            {
                StartTime = startTime,
                EndTime = endTime
            },
            Summary = statistics,
            DefectDistribution = defectDistribution,
            ConfidenceDistribution = confidenceDistribution,
            HourlyTrend = hourlyTrend,
            Recommendations = GenerateRecommendations(statistics, defectDistribution)
        };
    }

    /// <inheritdoc />
    public async Task<ComparisonAnalysisDto> ComparePeriodsAsync(Guid projectId, DateTime period1Start, DateTime period1End, DateTime period2Start, DateTime period2End)
    {
        var period1Stats = await GetStatisticsAsync(projectId, period1Start, period1End);
        var period2Stats = await GetStatisticsAsync(projectId, period2Start, period2End);

        var comparisons = new List<MetricComparisonDto>
        {
            new()
            {
                Metric = "总检测数",
                Period1Value = period1Stats.TotalCount,
                Period2Value = period2Stats.TotalCount,
                Change = CalculateChange(period1Stats.TotalCount, period2Stats.TotalCount),
                IsPositive = period2Stats.TotalCount >= period1Stats.TotalCount
            },
            new()
            {
                Metric = "OK率",
                Period1Value = period1Stats.OKRate * 100,
                Period2Value = period2Stats.OKRate * 100,
                Change = CalculateChange(period1Stats.OKRate, period2Stats.OKRate),
                IsPositive = period2Stats.OKRate >= period1Stats.OKRate
            },
            new()
            {
                Metric = "NG率",
                Period1Value = period1Stats.NGRate * 100,
                Period2Value = period2Stats.NGRate * 100,
                Change = CalculateChange(period1Stats.NGRate, period2Stats.NGRate),
                IsPositive = period2Stats.NGRate <= period1Stats.NGRate // NG率越低越好
            },
            new()
            {
                Metric = "平均处理时间(ms)",
                Period1Value = period1Stats.AverageProcessingTimeMs,
                Period2Value = period2Stats.AverageProcessingTimeMs,
                Change = CalculateChange(period1Stats.AverageProcessingTimeMs, period2Stats.AverageProcessingTimeMs),
                IsPositive = period2Stats.AverageProcessingTimeMs <= period1Stats.AverageProcessingTimeMs // 处理时间越短越好
            }
        };

        return new ComparisonAnalysisDto
        {
            ProjectId = projectId,
            Period1 = new ReportPeriodDto { StartTime = period1Start, EndTime = period1End },
            Period2 = new ReportPeriodDto { StartTime = period2Start, EndTime = period2End },
            Comparisons = comparisons,
            Summary = GenerateComparisonSummary(comparisons)
        };
    }

    /// <inheritdoc />
    public async Task<DefectHeatmapDto> GetDefectHeatmapAsync(Guid projectId, DateTime? startTime = null, DateTime? endTime = null)
    {
        var results = await _resultRepository.GetByTimeRangeAsync(projectId, startTime ?? DateTime.MinValue, endTime ?? DateTime.MaxValue);
        var allDefects = results.SelectMany(r => r.Defects).ToList();

        if (!allDefects.Any())
        {
            return new DefectHeatmapDto
            {
                ProjectId = projectId,
                StartTime = startTime,
                EndTime = endTime,
                TotalDefects = 0,
                GridSize = 10,
                Cells = new List<HeatmapCellDto>()
            };
        }

        // 计算边界
        var minX = allDefects.Min(d => d.X);
        var maxX = allDefects.Max(d => d.X + d.Width);
        var minY = allDefects.Min(d => d.Y);
        var maxY = allDefects.Max(d => d.Y + d.Height);

        var width = maxX - minX;
        var height = maxY - minY;

        // 创建10x10网格
        const int gridSize = 10;
        var cellWidth = width / gridSize;
        var cellHeight = height / gridSize;

        var cells = new List<HeatmapCellDto>();

        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                var cellMinX = minX + col * cellWidth;
                var cellMaxX = cellMinX + cellWidth;
                var cellMinY = minY + row * cellHeight;
                var cellMaxY = cellMinY + cellHeight;

                var count = allDefects.Count(d =>
                    d.X >= cellMinX && d.X < cellMaxX &&
                    d.Y >= cellMinY && d.Y < cellMaxY);

                cells.Add(new HeatmapCellDto
                {
                    Row = row,
                    Column = col,
                    X = cellMinX,
                    Y = cellMinY,
                    Width = cellWidth,
                    Height = cellHeight,
                    DefectCount = count,
                    Density = (double)count / allDefects.Count
                });
            }
        }

        return new DefectHeatmapDto
        {
            ProjectId = projectId,
            StartTime = startTime,
            EndTime = endTime,
            TotalDefects = allDefects.Count,
            ImageBounds = new BoundsDto { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY },
            GridSize = gridSize,
            Cells = cells
        };
    }

    #region Private Methods

    private List<string> GenerateRecommendations(InspectionStatisticsDto statistics, DefectDistributionDto defectDistribution)
    {
        var recommendations = new List<string>();

        if (statistics.OKRate < 0.8)
        {
            recommendations.Add($"OK率较低({statistics.OKRate:P1})，建议检查检测参数或光源配置");
        }

        if (statistics.AverageProcessingTimeMs > 500)
        {
            recommendations.Add($"平均处理时间较长({statistics.AverageProcessingTimeMs:F0}ms)，建议优化算子流程或降低图像分辨率");
        }

        if (defectDistribution.Items.Any())
        {
            var topDefect = defectDistribution.Items.First();
            recommendations.Add($"主要缺陷类型为{topDefect.DefectType}（{topDefect.Percentage:F1}%），建议重点关注");
        }

        if (statistics.ErrorRate > 0.05)
        {
            recommendations.Add($"错误率较高({statistics.ErrorRate:P1})，建议检查硬件连接和软件稳定性");
        }

        return recommendations;
    }

    private string GenerateComparisonSummary(List<MetricComparisonDto> comparisons)
    {
        var improvements = comparisons.Count(c => c.IsPositive);
        var total = comparisons.Count;
        
        return $"在{total}项指标中，有{improvements}项改善，{total - improvements}项下降";
    }

    private double CalculateChange(double oldValue, double newValue)
    {
        if (oldValue == 0) return newValue > 0 ? 100 : 0;
        return (newValue - oldValue) / oldValue * 100;
    }

    #endregion
}

/// <summary>
/// 时间间隔枚举
/// </summary>
public enum TrendInterval
{
    Hour,
    Day,
    Week,
    Month
}
