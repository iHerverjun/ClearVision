// FeatureMatchOperatorBase.cs
// 模板缓存结构
// 作者：蘅芜君

using System.Collections.Concurrent;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 特征匹配算子基类 - 支持AKAZE和ORB
/// </summary>
public abstract class FeatureMatchOperatorBase : OperatorBase
{
    // 模板缓存 - 使用ConcurrentDictionary支持线程安全
    protected static readonly ConcurrentDictionary<string, TemplateCacheEntry> TemplateCacheStore = new();

    protected FeatureMatchOperatorBase(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// 使用对称测试匹配描述符
    /// </summary>
    protected List<DMatch> MatchWithSymmetryTest(Mat templateDesc, Mat sceneDesc)
    {
        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
        
        // 正向匹配
        var forwardMatches = matcher.KnnMatch(templateDesc, sceneDesc, k: 2);
        // 反向匹配
        var backwardMatches = matcher.KnnMatch(sceneDesc, templateDesc, k: 2);

        // 构建反向最佳匹配字典
        var backwardBest = new Dictionary<int, int>();
        foreach (var m in backwardMatches)
        {
            if (m.Length >= 2 && m[0].Distance < 0.75 * m[1].Distance)
            {
                backwardBest[m[0].QueryIdx] = m[0].TrainIdx;
            }
            else if (m.Length == 1)
            {
                backwardBest[m[0].QueryIdx] = m[0].TrainIdx;
            }
        }

        // 对称测试筛选
        var goodMatches = new List<DMatch>();
        foreach (var m in forwardMatches)
        {
            if (m.Length < 2) continue;
            if (m[0].Distance >= 0.75 * m[1].Distance) continue;

            if (backwardBest.TryGetValue(m[0].TrainIdx, out int reverseTemplateIdx) && reverseTemplateIdx == m[0].QueryIdx)
            {
                goodMatches.Add(m[0]);
            }
        }

        return goodMatches;
    }

    /// <summary>
    /// 计算单应性矩阵
    /// </summary>
    protected (Mat? Homography, int Inliers) ComputeHomography(
        KeyPoint[] templateKeyPoints, 
        KeyPoint[] sceneKeyPoints, 
        List<DMatch> goodMatches)
    {
        if (goodMatches.Count < 4)
            return (null, 0);

        var srcPts = goodMatches.Select(m => templateKeyPoints[m.QueryIdx].Pt).ToArray();
        var dstPts = goodMatches.Select(m => sceneKeyPoints[m.TrainIdx].Pt).ToArray();

        using var mask = new Mat();
        var h = Cv2.FindHomography(
            InputArray.Create(srcPts),
            InputArray.Create(dstPts),
            HomographyMethods.Ransac,
            5.0,
            mask);

        int inliers = mask.Empty() ? 0 : Cv2.CountNonZero(mask);
        return (h, inliers);
    }

    /// <summary>
    /// 绘制透视框
    /// </summary>
    protected void DrawPerspectiveBox(Mat image, Mat homography, int templateWidth, int templateHeight, Scalar color)
    {
        var corners = new[] 
        { 
            new Point2f(0, 0), 
            new Point2f(templateWidth, 0), 
            new Point2f(templateWidth, templateHeight), 
            new Point2f(0, templateHeight) 
        };
        
        var projected = Cv2.PerspectiveTransform(corners, homography);
        var points = projected.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();

        double areaRatio = Math.Abs(Cv2.ContourArea(points)) / (double)(templateWidth * templateHeight);
        if (Cv2.IsContourConvex(points) && areaRatio > 0.1 && areaRatio < 4.0)
        {
            for (int i = 0; i < 4; i++)
                Cv2.Line(image, points[i], points[(i + 1) % 4], color, 3);
        }
    }

    /// <summary>
    /// 获取或加载模板
    /// </summary>
    protected (Mat Template, KeyPoint[] KeyPoints, Mat Descriptors)? GetOrLoadTemplate(string templatePath, Func<Mat, (KeyPoint[] KeyPoints, Mat Descriptors)> detector)
    {
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            return null;

        // 检查缓存
        if (TemplateCacheStore.TryGetValue(templatePath, out var cached))
        {
            return (cached.Template.Clone(), cached.KeyPoints, cached.Descriptors.Clone());
        }

        // 加载模板
        using var template = Cv2.ImRead(templatePath, ImreadModes.Color);
        if (template.Empty())
            return null;

        // 转换为灰度并检测特征
        using var gray = new Mat();
        if (template.Channels() > 1)
            Cv2.CvtColor(template, gray, ColorConversionCodes.BGR2GRAY);
        else
            template.CopyTo(gray);

        var (keyPoints, descriptors) = detector(gray);
        
        if (keyPoints.Length == 0 || descriptors.Empty())
            return null;

        // 添加到缓存
        TemplateCacheStore[templatePath] = new TemplateCacheEntry
        {
            Template = template.Clone(),
            KeyPoints = keyPoints,
            Descriptors = descriptors.Clone()
        };

        return (template.Clone(), keyPoints, descriptors.Clone());
    }

    /// <summary>
    /// 过滤特征点和描述符到最大数量
    /// </summary>
    protected (KeyPoint[] FilteredKeyPoints, Mat FilteredDescriptors) FilterFeatures(
        KeyPoint[] keyPoints, Mat descriptors, int maxFeatures)
    {
        if (keyPoints.Length <= maxFeatures)
            return (keyPoints, descriptors.Clone());

        // 按响应值排序
        var indices = Enumerable.Range(0, keyPoints.Length)
            .OrderByDescending(i => keyPoints[i].Response)
            .Take(maxFeatures)
            .ToArray();

        var filteredKpts = new KeyPoint[maxFeatures];
        var filteredDesc = new Mat(maxFeatures, descriptors.Cols, descriptors.Type());

        for (int i = 0; i < maxFeatures; i++)
        {
            int originalIdx = indices[i];
            filteredKpts[i] = keyPoints[originalIdx];
            using var srcRow = descriptors.Row(originalIdx);
            using var dstRow = filteredDesc.Row(i);
            srcRow.CopyTo(dstRow);
        }

        return (filteredKpts, filteredDesc);
    }

    /// <summary>
    /// 模板缓存结构
    /// </summary>
    protected class TemplateCacheEntry
    {
        public required Mat Template { get; set; }
        public required KeyPoint[] KeyPoints { get; set; }
        public required Mat Descriptors { get; set; }
    }
}
