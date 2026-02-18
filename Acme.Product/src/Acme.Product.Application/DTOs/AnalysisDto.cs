// AnalysisDto.cs
// 缺陷导出DTO
// 作者：蘅芜君

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 检测统计DTO
/// </summary>
public class InspectionStatisticsDto
{
    public Guid ProjectId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalCount { get; set; }
    public int OKCount { get; set; }
    public int NGCount { get; set; }
    public int ErrorCount { get; set; }
    public double OKRate { get; set; }
    public double NGRate { get; set; }
    public double ErrorRate { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public int TotalDefects { get; set; }
}

/// <summary>
/// 缺陷分布DTO
/// </summary>
public class DefectDistributionDto
{
    public Guid ProjectId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalDefects { get; set; }
    public List<DefectDistributionItemDto> Items { get; set; } = new();
}

/// <summary>
/// 缺陷分布项DTO
/// </summary>
public class DefectDistributionItemDto
{
    public string DefectType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// 置信度分布DTO
/// </summary>
public class ConfidenceDistributionDto
{
    public Guid ProjectId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalDefects { get; set; }
    public List<ConfidenceBucketDto> Buckets { get; set; } = new();
    public double AverageConfidence { get; set; }
}

/// <summary>
/// 置信度分桶DTO
/// </summary>
public class ConfidenceBucketDto
{
    public string Range { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// 趋势分析DTO
/// </summary>
public class TrendAnalysisDto
{
    public Guid ProjectId { get; set; }
    public string Interval { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<TrendDataPointDto> DataPoints { get; set; } = new();
}

/// <summary>
/// 趋势数据点DTO
/// </summary>
public class TrendDataPointDto
{
    public DateTime Timestamp { get; set; }
    public int TotalCount { get; set; }
    public int OKCount { get; set; }
    public int NGCount { get; set; }
    public int ErrorCount { get; set; }
    public double OKRate { get; set; }
    public int DefectCount { get; set; }
    public double AverageProcessingTime { get; set; }
}

/// <summary>
/// 检测报告DTO
/// </summary>
public class InspectionReportDto
{
    public Guid ProjectId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public ReportPeriodDto Period { get; set; } = new();
    public InspectionStatisticsDto Summary { get; set; } = new();
    public DefectDistributionDto DefectDistribution { get; set; } = new();
    public ConfidenceDistributionDto ConfidenceDistribution { get; set; } = new();
    public TrendAnalysisDto HourlyTrend { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// 报告周期DTO
/// </summary>
public class ReportPeriodDto
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 对比分析DTO
/// </summary>
public class ComparisonAnalysisDto
{
    public Guid ProjectId { get; set; }
    public ReportPeriodDto Period1 { get; set; } = new();
    public ReportPeriodDto Period2 { get; set; } = new();
    public List<MetricComparisonDto> Comparisons { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// 指标对比DTO
/// </summary>
public class MetricComparisonDto
{
    public string Metric { get; set; } = string.Empty;
    public double Period1Value { get; set; }
    public double Period2Value { get; set; }
    public double Change { get; set; }
    public bool IsPositive { get; set; }
}

/// <summary>
/// 缺陷热点图DTO
/// </summary>
public class DefectHeatmapDto
{
    public Guid ProjectId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalDefects { get; set; }
    public BoundsDto? ImageBounds { get; set; }
    public int GridSize { get; set; }
    public List<HeatmapCellDto> Cells { get; set; } = new();
}

/// <summary>
/// 边界DTO
/// </summary>
public class BoundsDto
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
}

/// <summary>
/// 热点图单元格DTO
/// </summary>
public class HeatmapCellDto
{
    public int Row { get; set; }
    public int Column { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int DefectCount { get; set; }
    public double Density { get; set; }
}

/// <summary>
/// 检测导出DTO
/// </summary>
public class InspectionExportDto
{
    public Guid ProjectId { get; set; }
    public DateTime ExportTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<InspectionResultExportItemDto> Results { get; set; } = new();
}

/// <summary>
/// 检测结果导出项DTO
/// </summary>
public class InspectionResultExportItemDto
{
    public Guid Id { get; set; }
    public DateTime InspectionTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DefectExportDto> Defects { get; set; } = new();
}

/// <summary>
/// 缺陷导出DTO
/// </summary>
public class DefectExportDto
{
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double ConfidenceScore { get; set; }
    public string? Description { get; set; }
}
