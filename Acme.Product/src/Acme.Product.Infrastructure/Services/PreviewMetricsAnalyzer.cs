// PreviewMetricsAnalyzer.cs
// 预览结果指标分析器
// 【Phase 4】LLM 闭环验证 - 提取指标和诊断
// 作者：架构修复方案 v2

using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 预览结果指标分析器接口
/// </summary>
public interface IPreviewMetricsAnalyzer
{
    /// <summary>
    /// 分析预览结果，提取指标和诊断
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="outputData">算子输出数据</param>
    /// <param name="goal">调参目标</param>
    /// <returns>预览指标</returns>
    PreviewMetrics Analyze(Mat image, Dictionary<string, object>? outputData, AutoTuneGoal? goal = null);
}

/// <summary>
/// 预览结果指标分析器实现
/// </summary>
public class PreviewMetricsAnalyzer : IPreviewMetricsAnalyzer
{
    private readonly ILogger<PreviewMetricsAnalyzer> _logger;

    public PreviewMetricsAnalyzer(ILogger<PreviewMetricsAnalyzer> logger)
    {
        _logger = logger;
    }

    public PreviewMetrics Analyze(Mat image, Dictionary<string, object>? outputData, AutoTuneGoal? goal = null)
    {
        var metrics = new PreviewMetrics();

        try
        {
            // 1. 图像统计
            metrics.ImageStats = CalculateImageStats(image);

            // 2. Blob 统计
            metrics.BlobStats = ExtractBlobStats(outputData);

            // 3. 诊断标签
            metrics.Diagnostics = GenerateDiagnostics(image, metrics.BlobStats, metrics.ImageStats);

            // 4. 可优化目标与评分
            metrics.Goals = CalculateGoals(metrics.BlobStats, goal);

            // 5. 综合评分
            metrics.OverallScore = CalculateOverallScore(metrics, goal);

            // 6. 参数建议
            metrics.Suggestions = GenerateSuggestions(metrics, outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PreviewMetricsAnalyzer] 分析预览结果失败");
        }

        return metrics;
    }

    /// <summary>
    /// 计算图像统计
    /// </summary>
    private ImageStats CalculateImageStats(Mat image)
    {
        var stats = new ImageStats();

        // 转换为灰度图（如果不是）
        Mat grayImage;
        if (image.Channels() == 1)
        {
            grayImage = image.Clone();
        }
        else
        {
            grayImage = new Mat();
            Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);
        }

        // 计算均值和标准差
        Mat mean = new Mat();
        Mat stddev = new Mat();
        Cv2.MeanStdDev(grayImage, mean, stddev);
        stats.MeanIntensity = mean.At<double>(0);
        stats.StdDev = stddev.At<double>(0);

        // 计算拉普拉斯方差（清晰度指标）
        Mat laplacian = new Mat();
        Cv2.Laplacian(grayImage, laplacian, MatType.CV_64F);
        Mat lapMean = new Mat();
        Mat lapStddev = new Mat();
        Cv2.MeanStdDev(laplacian, lapMean, lapStddev);
        stats.LaplacianVariance = lapStddev.At<double>(0) * lapStddev.At<double>(0);

        // 计算直方图
        Mat hist = new Mat();
        int[] channels = { 0 };
        int[] histSize = { 256 };
        Rangef[] ranges = { new Rangef(0, 256) };
        Cv2.CalcHist(new[] { grayImage }, channels, null, hist, 1, histSize, ranges);
        for (int i = 0; i < 256; i++)
        {
            stats.Histogram[i] = (int)hist.At<float>(i);
        }

        // 计算最小、最大、中位数
        Cv2.MinMaxLoc(grayImage, out double minVal, out double maxVal);
        stats.MinIntensity = minVal;
        stats.MaxIntensity = maxVal;

        // 计算中位数
        stats.MedianIntensity = CalculateMedian(grayImage);

        // 释放资源
        if (grayImage != image)
        {
            grayImage.Dispose();
        }
        mean.Dispose();
        stddev.Dispose();
        laplacian.Dispose();
        lapMean.Dispose();
        lapStddev.Dispose();
        hist.Dispose();

        return stats;
    }

    /// <summary>
    /// 提取 Blob 统计
    /// </summary>
    private List<BlobStat> ExtractBlobStats(Dictionary<string, object>? outputData)
    {
        var blobStats = new List<BlobStat>();

        if (outputData == null)
            return blobStats;

        // 尝试从 Defects 提取
        if (outputData.TryGetValue("Defects", out var defectsObj) && defectsObj is System.Collections.IList defectsList)
        {
            int id = 0;
            foreach (var item in defectsList)
            {
                if (item is Dictionary<string, object> defectDict)
                {
                    var stat = new BlobStat
                    {
                        Id = id++,
                        CentroidX = Convert.ToDouble(defectDict.GetValueOrDefault("X", 0)),
                        CentroidY = Convert.ToDouble(defectDict.GetValueOrDefault("Y", 0)),
                        BoundingBox = new BoundingBox
                        {
                            X = Convert.ToDouble(defectDict.GetValueOrDefault("X", 0)),
                            Y = Convert.ToDouble(defectDict.GetValueOrDefault("Y", 0)),
                            Width = Convert.ToDouble(defectDict.GetValueOrDefault("Width", 0)),
                            Height = Convert.ToDouble(defectDict.GetValueOrDefault("Height", 0))
                        },
                        Area = Convert.ToDouble(defectDict.GetValueOrDefault("Area", 
                            Convert.ToDouble(defectDict.GetValueOrDefault("Width", 0)) * 
                            Convert.ToDouble(defectDict.GetValueOrDefault("Height", 0)))),
                        RejectReason = defectDict.GetValueOrDefault("RejectReason", null)?.ToString()
                    };

                    // 计算圆形度（如果有面积和周长）
                    if (defectDict.TryGetValue("Perimeter", out var perimObj) && perimObj is double perimeter && perimeter > 0)
                    {
                        stat.Perimeter = perimeter;
                        double expectedPerimeter = 2 * Math.Sqrt(stat.Area * Math.PI);
                        stat.Circularity = expectedPerimeter / perimeter;
                    }
                    else
                    {
                        // 根据宽高比估算
                        double aspectRatio = stat.BoundingBox.Width / Math.Max(stat.BoundingBox.Height, 1);
                        stat.Circularity = 1.0 / Math.Max(aspectRatio, 1.0 / aspectRatio);
                    }

                    blobStats.Add(stat);
                }
            }
        }

        // 尝试从 Blobs 提取
        if (outputData.TryGetValue("Blobs", out var blobsObj) && blobsObj is System.Collections.IList blobsList)
        {
            // 类似处理...
        }

        return blobStats;
    }

    /// <summary>
    /// 生成诊断标签
    /// </summary>
    private List<string> GenerateDiagnostics(Mat image, List<BlobStat> blobStats, ImageStats imageStats)
    {
        var diagnostics = new List<string>();

        // 1. 检查反光（高亮区域占比）
        double highIntensityRatio = imageStats.Histogram.Skip(200).Sum() / (double)imageStats.Histogram.Sum();
        if (highIntensityRatio > 0.3)
        {
            diagnostics.Add("SpecularHighlightsDominant");
        }

        // 2. 检查噪声（小 Blob 数量）
        int smallBlobs = blobStats.Count(b => b.Area < 50);
        if (smallBlobs > blobStats.Count * 0.5 && blobStats.Count > 5)
        {
            diagnostics.Add("MaskTooNoisy");
        }

        // 3. 检查碎片化（Blob 数量异常多）
        if (blobStats.Count > 20)
        {
            diagnostics.Add("StrapFragmented");
        }

        // 4. 检查对比度
        if (imageStats.StdDev < 30)
        {
            diagnostics.Add("LowContrast");
        }

        // 5. 检查清晰度
        if (imageStats.LaplacianVariance < 100)
        {
            diagnostics.Add("BlurryImage");
        }

        // 6. 检查光照不均
        double cornerMean = CalculateCornerMean(image);
        if (Math.Abs(cornerMean - imageStats.MeanIntensity) > 50)
        {
            diagnostics.Add("UnevenIllumination");
        }

        return diagnostics;
    }

    /// <summary>
    /// 计算可优化目标与评分
    /// </summary>
    private OptimizationGoals CalculateGoals(List<BlobStat> blobStats, AutoTuneGoal? goal)
    {
        var goals = new OptimizationGoals
        {
            CurrentBlobCount = blobStats.Count,
            TargetBlobCount = goal?.TargetBlobCount
        };

        // 计算数量误差
        if (goal?.TargetBlobCount.HasValue == true)
        {
            goals.CountError = Math.Abs(blobStats.Count - goal.TargetBlobCount.Value) / (double)goal.TargetBlobCount.Value;
        }
        else
        {
            goals.CountError = 0;
        }

        // 计算噪声惩罚（小于 MinArea 的 Blob）
        if (goal?.MinArea.HasValue == true)
        {
            goals.NoisePenalty = blobStats.Count(b => b.Area < goal.MinArea.Value);
        }

        // 计算碎片化惩罚（形状不规则的 Blob）
        goals.FragmentPenalty = blobStats.Count(b => b.Circularity < 0.5);

        // 计算面积分布评分（方差越小越均匀）
        if (blobStats.Count > 1)
        {
            var areas = blobStats.Select(b => b.Area).ToList();
            double meanArea = areas.Average();
            double variance = areas.Select(a => (a - meanArea) * (a - meanArea)).Average();
            double cv = Math.Sqrt(variance) / meanArea; // 变异系数
            goals.AreaDistributionScore = Math.Max(0, 1 - cv);
        }
        else
        {
            goals.AreaDistributionScore = 1;
        }

        // 计算形状规则度评分
        if (blobStats.Count > 0)
        {
            goals.ShapeRegularityScore = blobStats.Average(b => b.Circularity);
        }
        else
        {
            goals.ShapeRegularityScore = 0;
        }

        return goals;
    }

    /// <summary>
    /// 计算综合评分
    /// </summary>
    private double CalculateOverallScore(PreviewMetrics metrics, AutoTuneGoal? goal)
    {
        if (metrics.BlobStats.Count == 0)
            return 0;

        double score = 0;
        int weightCount = 0;

        // 数量匹配度（权重 40%）
        if (goal?.TargetBlobCount.HasValue == true)
        {
            double countScore = 1 - Math.Min(metrics.Goals.CountError, 1);
            score += countScore * 0.4;
            weightCount++;
        }

        // 噪声控制（权重 20%）
        double noiseRatio = metrics.Goals.NoisePenalty / Math.Max(metrics.BlobStats.Count, 1);
        double noiseScore = 1 - Math.Min(noiseRatio, 1);
        score += noiseScore * 0.2;
        weightCount++;

        // 形状规则度（权重 20%）
        score += metrics.Goals.ShapeRegularityScore * 0.2;
        weightCount++;

        // 面积分布（权重 20%）
        score += metrics.Goals.AreaDistributionScore * 0.2;
        weightCount++;

        return score;
    }

    /// <summary>
    /// 生成参数建议
    /// </summary>
    private List<ParameterSuggestion> GenerateSuggestions(PreviewMetrics metrics, Dictionary<string, object>? outputData)
    {
        var suggestions = new List<ParameterSuggestion>();

        // 根据诊断标签生成建议
        foreach (var diagnostic in metrics.Diagnostics)
        {
            switch (diagnostic)
            {
                case "SpecularHighlightsDominant":
                    suggestions.Add(new ParameterSuggestion
                    {
                        ParameterName = "Threshold",
                        SuggestedValue = "增加",
                        Reason = "高亮区域占比过高，可能导致误检",
                        ExpectedImprovement = "减少反光引起的误检"
                    });
                    break;

                case "MaskTooNoisy":
                    suggestions.Add(new ParameterSuggestion
                    {
                        ParameterName = "MinArea",
                        SuggestedValue = "增加",
                        Reason = "检测到过多小碎片",
                        ExpectedImprovement = "过滤噪声Blob"
                    });
                    suggestions.Add(new ParameterSuggestion
                    {
                        ParameterName = "MorphologyOperation",
                        SuggestedValue = "Opening",
                        Reason = "噪声较多",
                        ExpectedImprovement = "去除小噪点"
                    });
                    break;

                case "StrapFragmented":
                    suggestions.Add(new ParameterSuggestion
                    {
                        ParameterName = "MorphologyOperation",
                        SuggestedValue = "Closing",
                        Reason = "Blob被过度分割",
                        ExpectedImprovement = "连接被分割的Blob"
                    });
                    break;

                case "LowContrast":
                    suggestions.Add(new ParameterSuggestion
                    {
                        ParameterName = "ClaheEnhancement",
                        SuggestedValue = "启用",
                        Reason = "图像对比度低",
                        ExpectedImprovement = "增强局部对比度"
                    });
                    break;
            }
        }

        // 根据数量误差生成建议
        if (metrics.Goals.CountError > 0.2)
        {
            if (metrics.BlobStats.Count > (metrics.Goals.TargetBlobCount ?? 0))
            {
                suggestions.Add(new ParameterSuggestion
                {
                    ParameterName = "Threshold",
                    SuggestedValue = "增加",
                    Reason = $"Blob数量过多 ({metrics.BlobStats.Count} vs 目标 {metrics.Goals.TargetBlobCount})",
                    ExpectedImprovement = "减少检测到的Blob数量"
                });
            }
            else
            {
                suggestions.Add(new ParameterSuggestion
                {
                    ParameterName = "Threshold",
                    SuggestedValue = "降低",
                    Reason = $"Blob数量不足 ({metrics.BlobStats.Count} vs 目标 {metrics.Goals.TargetBlobCount})",
                    ExpectedImprovement = "增加检测到的Blob数量"
                });
            }
        }

        return suggestions;
    }

    /// <summary>
    /// 计算中位数
    /// </summary>
    private double CalculateMedian(Mat image)
    {
        // 将图像数据展平并排序
        var data = new byte[image.Total()];
        Marshal.Copy(image.Data, data, 0, data.Length);
        Array.Sort(data);
        
        if (data.Length % 2 == 0)
        {
            return (data[data.Length / 2 - 1] + data[data.Length / 2]) / 2.0;
        }
        else
        {
            return data[data.Length / 2];
        }
    }

    /// <summary>
    /// 计算图像四个角的平均亮度（用于检测光照不均）
    /// </summary>
    private double CalculateCornerMean(Mat image)
    {
        int w = image.Width;
        int h = image.Height;
        int cornerSize = Math.Min(w, h) / 10;

        var corners = new List<OpenCvSharp.Rect>
        {
            new OpenCvSharp.Rect(0, 0, cornerSize, cornerSize),  // 左上
            new OpenCvSharp.Rect(w - cornerSize, 0, cornerSize, cornerSize),  // 右上
            new OpenCvSharp.Rect(0, h - cornerSize, cornerSize, cornerSize),  // 左下
            new OpenCvSharp.Rect(w - cornerSize, h - cornerSize, cornerSize, cornerSize)  // 右下
        };

        double cornerSum = 0;
        foreach (var rect in corners)
        {
            using var corner = new Mat(image, rect);
            cornerSum += Cv2.Mean(corner).Val0;
        }

        return cornerSum / 4;
    }
}
