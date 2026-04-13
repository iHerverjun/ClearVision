// FeatureMatchOperatorBase.cs
// 妯℃澘缂撳瓨缁撴瀯
// 浣滆€咃細铇呰姕鍚?
using System.Security.Cryptography;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 鐗瑰緛鍖归厤绠楀瓙鍩虹被 - 鏀寔AKAZE鍜孫RB
/// </summary>
public abstract class FeatureMatchOperatorBase : OperatorBase
{
    private const int TemplateCacheCapacity = 16;
    protected static readonly Dictionary<string, TemplateCacheEntry> TemplateCacheStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> TemplateCacheOrder = new();
    private static readonly object TemplateCacheLock = new();

    protected FeatureMatchOperatorBase(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// 浣跨敤瀵圭О娴嬭瘯鍖归厤鎻忚堪绗?    /// </summary>
    protected List<DMatch> MatchWithSymmetryTest(Mat templateDesc, Mat sceneDesc)
    {
        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);

        // 姝ｅ悜鍖归厤
        var forwardMatches = matcher.KnnMatch(templateDesc, sceneDesc, k: 2);
        // 鍙嶅悜鍖归厤
        var backwardMatches = matcher.KnnMatch(sceneDesc, templateDesc, k: 2);

        // 鏋勫缓鍙嶅悜鏈€浣冲尮閰嶅瓧鍏?
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

        // 瀵圭О娴嬭瘯绛涢€?
        var goodMatches = new List<DMatch>();
        foreach (var m in forwardMatches)
        {
            if (m.Length < 2) continue;
            if (m[0].Distance >= 0.75 * m[1].Distance) continue;

            if (backwardBest.TryGetValue(m[0].TrainIdx, out var reverseTemplateIdx) && reverseTemplateIdx == m[0].QueryIdx)
            {
                goodMatches.Add(m[0]);
            }
        }

        return goodMatches;
    }

    /// <summary>
    /// 璁＄畻鍗曞簲鎬х煩闃?
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

        var inliers = mask.Empty() ? 0 : Cv2.CountNonZero(mask);
        return (h, inliers);
    }

    /// <summary>
    /// 缁樺埗閫忚妗?
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

        var areaRatio = Math.Abs(Cv2.ContourArea(points)) / (double)(templateWidth * templateHeight);
        if (Cv2.IsContourConvex(points) && areaRatio > 0.1 && areaRatio < 4.0)
        {
            for (var i = 0; i < 4; i++)
                Cv2.Line(image, points[i], points[(i + 1) % 4], color, 3);
        }
    }

    /// <summary>
    /// 鑾峰彇鎴栧姞杞芥ā鏉?
    /// </summary>
    protected (Mat Template, KeyPoint[] KeyPoints, Mat Descriptors)? GetOrLoadTemplate(
        string templatePath,
        string cacheDiscriminator,
        Func<Mat, (KeyPoint[] KeyPoints, Mat Descriptors)> detector)
    {
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            return null;

        var fingerprint = ComputeFileFingerprint(templatePath);
        var cacheKey = $"{templatePath}|{cacheDiscriminator}|{fingerprint}";

        if (TryGetCachedTemplate(cacheKey, out var cached))
        {
            return cached;
        }

        // 鍔犺浇妯℃澘
        using var template = Cv2.ImRead(templatePath, ImreadModes.Color);
        if (template.Empty())
            return null;

        // 杞崲涓虹伆搴﹀苟妫€娴嬬壒寰?
        using var gray = new Mat();
        if (template.Channels() > 1)
            Cv2.CvtColor(template, gray, ColorConversionCodes.BGR2GRAY);
        else
            template.CopyTo(gray);

        var (keyPoints, descriptors) = detector(gray);

        if (keyPoints.Length == 0 || descriptors.Empty())
        {
            descriptors.Dispose();
            return null;
        }

        var entry = new TemplateCacheEntry
        {
            Template = template.Clone(),
            KeyPoints = keyPoints.ToArray(),
            Descriptors = descriptors.Clone()
        };
        descriptors.Dispose();

        AddTemplateCacheEntry(cacheKey, entry);
        return CloneTemplateEntry(entry);
    }

    /// <summary>
    /// 杩囨护鐗瑰緛鐐瑰拰鎻忚堪绗﹀埌鏈€澶ф暟閲?
    /// </summary>
    protected (KeyPoint[] FilteredKeyPoints, Mat FilteredDescriptors) FilterFeatures(
        KeyPoint[] keyPoints, Mat descriptors, int maxFeatures)
    {
        if (keyPoints.Length <= maxFeatures)
            return (keyPoints, descriptors.Clone());

        // 鎸夊搷搴斿€兼帓搴?
        var indices = Enumerable.Range(0, keyPoints.Length)
            .OrderByDescending(i => keyPoints[i].Response)
            .Take(maxFeatures)
            .ToArray();

        var filteredKpts = new KeyPoint[maxFeatures];
        var filteredDesc = new Mat(maxFeatures, descriptors.Cols, descriptors.Type());

        for (var i = 0; i < maxFeatures; i++)
        {
            var originalIdx = indices[i];
            filteredKpts[i] = keyPoints[originalIdx];
            using var srcRow = descriptors.Row(originalIdx);
            using var dstRow = filteredDesc.Row(i);
            srcRow.CopyTo(dstRow);
        }

        return (filteredKpts, filteredDesc);
    }

    /// <summary>
    /// 妯℃澘缂撳瓨缁撴瀯
    /// </summary>
    protected class TemplateCacheEntry
    {
        public required Mat Template { get; set; }
        public required KeyPoint[] KeyPoints { get; set; }
        public required Mat Descriptors { get; set; }
        public LinkedListNode<string>? OrderNode { get; set; }
    }

    private static bool TryGetCachedTemplate(string cacheKey, out (Mat Template, KeyPoint[] KeyPoints, Mat Descriptors) cached)
    {
        lock (TemplateCacheLock)
        {
            if (TemplateCacheStore.TryGetValue(cacheKey, out var entry))
            {
                TouchTemplateCacheEntry(cacheKey, entry);
                cached = CloneTemplateEntry(entry);
                return true;
            }
        }

        cached = default;
        return false;
    }

    private static void AddTemplateCacheEntry(string cacheKey, TemplateCacheEntry entry)
    {
        lock (TemplateCacheLock)
        {
            if (TemplateCacheStore.TryGetValue(cacheKey, out var existing))
            {
                entry.Template.Dispose();
                entry.Descriptors.Dispose();
                TouchTemplateCacheEntry(cacheKey, existing);
                return;
            }

            while (TemplateCacheStore.Count >= TemplateCacheCapacity)
            {
                var oldestKey = TemplateCacheOrder.First?.Value;
                if (oldestKey == null)
                {
                    break;
                }

                if (TemplateCacheStore.Remove(oldestKey, out var evicted))
                {
                    TemplateCacheOrder.RemoveFirst();
                    evicted.Template.Dispose();
                    evicted.Descriptors.Dispose();
                }
            }

            entry.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
            TemplateCacheStore[cacheKey] = entry;
        }
    }

    private static void TouchTemplateCacheEntry(string cacheKey, TemplateCacheEntry entry)
    {
        if (entry.OrderNode != null)
        {
            TemplateCacheOrder.Remove(entry.OrderNode);
        }

        entry.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
    }

    private static (Mat Template, KeyPoint[] KeyPoints, Mat Descriptors) CloneTemplateEntry(TemplateCacheEntry entry)
    {
        return (entry.Template.Clone(), entry.KeyPoints.ToArray(), entry.Descriptors.Clone());
    }

    private static string ComputeFileFingerprint(string templatePath)
    {
        using var stream = File.OpenRead(templatePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
