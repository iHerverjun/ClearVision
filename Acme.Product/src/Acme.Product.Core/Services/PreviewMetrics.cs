// PreviewMetrics.cs
// 预览结果指标和诊断数据
// 【Phase 4】LLM 闭环验证 - 扩展预览反馈信息
// 作者：架构修复方案 v2

namespace Acme.Product.Core.Services;

/// <summary>
/// 预览结果指标
/// 包含图像统计、Blob 统计、诊断标签和可优化目标
/// </summary>
public class PreviewMetrics
{
    /// <summary>
    /// 图像统计信息
    /// </summary>
    public ImageStats ImageStats { get; set; } = new();

    /// <summary>
    /// Blob 统计信息
    /// </summary>
    public List<BlobStat> BlobStats { get; set; } = new();

    /// <summary>
    /// 诊断标签（如反光、噪声、碎片化）
    /// </summary>
    public List<string> Diagnostics { get; set; } = new();

    /// <summary>
    /// 可优化目标与评分
    /// </summary>
    public OptimizationGoals Goals { get; set; } = new();

    /// <summary>
    /// 综合评分（0-1）
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 建议的下一步参数调整
    /// </summary>
    public List<ParameterSuggestion> Suggestions { get; set; } = new();
}

/// <summary>
/// 图像统计
/// </summary>
public class ImageStats
{
    /// <summary>
    /// 平均亮度
    /// </summary>
    public double MeanIntensity { get; set; }

    /// <summary>
    /// 标准差（对比度指标）
    /// </summary>
    public double StdDev { get; set; }

    /// <summary>
    /// 拉普拉斯方差（清晰度指标）
    /// </summary>
    public double LaplacianVariance { get; set; }

    /// <summary>
    /// 直方图数据（256 个灰度级）
    /// </summary>
    public int[] Histogram { get; set; } = new int[256];

    /// <summary>
    /// 最小灰度值
    /// </summary>
    public double MinIntensity { get; set; }

    /// <summary>
    /// 最大灰度值
    /// </summary>
    public double MaxIntensity { get; set; }

    /// <summary>
    /// 中位数灰度值
    /// </summary>
    public double MedianIntensity { get; set; }
}

/// <summary>
/// Blob 统计
/// </summary>
public class BlobStat
{
    /// <summary>
    /// Blob ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 面积（像素数）
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 周长
    /// </summary>
    public double Perimeter { get; set; }

    /// <summary>
    /// 圆形度（0-1，越接近 1 越圆）
    /// </summary>
    public double Circularity { get; set; }

    /// <summary>
    /// 重心 X
    /// </summary>
    public double CentroidX { get; set; }

    /// <summary>
    /// 重心 Y
    /// </summary>
    public double CentroidY { get; set; }

    /// <summary>
    /// 边界框
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>
    /// 被拒绝的原因（如太小、太扁等）
    /// </summary>
    public string? RejectReason { get; set; }
}

/// <summary>
/// 边界框
/// </summary>
public class BoundingBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// 可优化目标与评分
/// </summary>
public class OptimizationGoals
{
    /// <summary>
    /// 目标 Blob 数量
    /// </summary>
    public int? TargetBlobCount { get; set; }

    /// <summary>
    /// 当前 Blob 数量
    /// </summary>
    public int CurrentBlobCount { get; set; }

    /// <summary>
    /// 数量误差（越小越好）
    /// </summary>
    public double CountError { get; set; }

    /// <summary>
    /// 噪声惩罚（检测到的小碎片数量）
    /// </summary>
    public int NoisePenalty { get; set; }

    /// <summary>
    /// 碎片化惩罚（Blob 被分割的数量）
    /// </summary>
    public int FragmentPenalty { get; set; }

    /// <summary>
    /// 面积分布评分（0-1，越均匀越好）
    /// </summary>
    public double AreaDistributionScore { get; set; }

    /// <summary>
    /// 形状规则度评分（0-1，越规则越好）
    /// </summary>
    public double ShapeRegularityScore { get; set; }
}

/// <summary>
/// 参数建议
/// </summary>
public class ParameterSuggestion
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// 当前值
    /// </summary>
    public object? CurrentValue { get; set; }

    /// <summary>
    /// 建议值
    /// </summary>
    public object? SuggestedValue { get; set; }

    /// <summary>
    /// 调整原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 预期改进
    /// </summary>
    public string ExpectedImprovement { get; set; } = string.Empty;
}

/// <summary>
/// 自动调参目标
/// </summary>
public class AutoTuneGoal
{
    /// <summary>
    /// 目标 Blob 数量
    /// </summary>
    public int? TargetBlobCount { get; set; }

    /// <summary>
    /// Blob 数量容差（默认 0.1 = 10%）
    /// </summary>
    public double Tolerance { get; set; } = 0.1;

    /// <summary>
    /// 最小 Blob 面积
    /// </summary>
    public double? MinArea { get; set; }

    /// <summary>
    /// 最大 Blob 面积
    /// </summary>
    public double? MaxArea { get; set; }

    /// <summary>
    /// 形状要求（如 "round", "rectangular"）
    /// </summary>
    public string? ShapeRequirement { get; set; }
}

/// <summary>
/// 自动调参结果
/// </summary>
public class AutoTuneResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 最终参数
    /// </summary>
    public Dictionary<string, object> FinalParameters { get; set; } = new();

    /// <summary>
    /// 最终评分
    /// </summary>
    public double FinalScore { get; set; }

    /// <summary>
    /// 迭代历史
    /// </summary>
    public List<AutoTuneIteration> Iterations { get; set; } = new();

    /// <summary>
    /// 执行的总迭代次数
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// 是否达到目标
    /// </summary>
    public bool IsGoalAchieved { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 自动调参迭代记录
/// </summary>
public class AutoTuneIteration
{
    /// <summary>
    /// 迭代序号
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// 本轮参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 本轮指标
    /// </summary>
    public PreviewMetrics Metrics { get; set; } = new();

    /// <summary>
    /// 本轮评分
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}
