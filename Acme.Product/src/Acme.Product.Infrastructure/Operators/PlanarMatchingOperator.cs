// PlanarMatchingOperator.cs
// 透视匹配算子 - 基于特征匹配和单应性估计的平面物体检测
// 对标 Halcon: find_planar_uncalib_deformable_model
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Security.Cryptography;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 透视匹配算子 - 基于特征匹配和单应性估计的平面物体检测
/// 对标 Halcon find_planar_uncalib_deformable_model
/// </summary>
[OperatorMeta(
    DisplayName = "Planar Matching",
    Description = "Feature-based planar matching with homography verification. Suitable for textured planar targets under perspective change.",
    Category = "Matching",
    IconName = "planar-match",
    Keywords = new[] { "Planar", "Matching", "Homography", "Perspective", "ORB", "AKAZE", "RANSAC" },
    Version = "1.1.1"
)]
[InputPort("Image", "Search Image", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "Template Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("IsMatch", "Is Match", PortDataType.Boolean)]
[OutputPort("Score", "Score", PortDataType.Float)]
[OutputPort("MatchCount", "Match Count", PortDataType.Integer)]
[OutputPort("Method", "Method", PortDataType.String)]
[OutputPort("FailureReason", "Failure Reason", PortDataType.String)]
[OutputPort("CandidateScore", "Candidate Score", PortDataType.Float)]
[OutputPort("InlierCount", "Inlier Count", PortDataType.Integer)]
[OutputPort("InlierRatio", "Inlier Ratio", PortDataType.Float)]
[OutputPort("VerificationPassed", "Verification Passed", PortDataType.Boolean)]
[OutputPort("MatchResult", "Match Result", PortDataType.Any)]
[OutputPort("Homography", "Homography Matrix", PortDataType.Any)]
[OutputPort("Corners", "Detected Corners", PortDataType.PointList)]
[OperatorParam("TemplatePath", "Template Image Path", "file", DefaultValue = "")]
[OperatorParam("DetectorType", "Feature Detector", "enum", DefaultValue = "ORB", Options = new[] { "ORB|ORB", "AKAZE|AKAZE", "BRISK|BRISK" })]
[OperatorParam("MaxFeatures", "Max Features", "int", DefaultValue = 1000, Min = 100, Max = 5000)]
[OperatorParam("ScaleFactor", "Scale Factor", "double", DefaultValue = 1.2, Min = 1.01, Max = 2.0)]
[OperatorParam("NLevels", "Pyramid Levels", "int", DefaultValue = 8, Min = 1, Max = 16)]
[OperatorParam("MatchRatio", "Match Ratio (Lowe's)", "double", DefaultValue = 0.75, Min = 0.5, Max = 0.95)]
[OperatorParam("RansacThreshold", "RANSAC Threshold (px)", "double", DefaultValue = 3.0, Min = 0.5, Max = 10.0)]
[OperatorParam("MinMatchCount", "Min Match Count", "int", DefaultValue = 10, Min = 4, Max = 100)]
[OperatorParam("MinInliers", "Min Inliers", "int", DefaultValue = 8, Min = 4, Max = 100)]
[OperatorParam("MinInlierRatio", "Min Inlier Ratio", "double", DefaultValue = 0.25, Min = 0.1, Max = 1.0)]
[OperatorParam("ScoreThreshold", "Score Threshold", "double", DefaultValue = 0.5, Min = 0.0, Max = 1.0)]
[OperatorParam("UseRoi", "Use ROI", "bool", DefaultValue = false)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiWidth", "ROI Width", "int", DefaultValue = 0)]
[OperatorParam("RoiHeight", "ROI Height", "int", DefaultValue = 0)]
[OperatorParam("EnableMultiScale", "Enable Multi-Scale", "bool", DefaultValue = true)]
[OperatorParam("ScaleRange", "Scale Range (±)", "double", DefaultValue = 0.2, Min = 0.0, Max = 1.0)]
[OperatorParam("EnableEarlyExit", "Enable Early Exit", "bool", DefaultValue = false)]
public class PlanarMatchingOperator : OperatorBase
{
    private static readonly string[] SupportedDetectorTypes = { "ORB", "AKAZE", "BRISK" };
    private const int TemplateCacheCapacity = 20;

    public override OperatorType OperatorType => OperatorType.PlanarMatching;

    // 模板特征缓存
    private static readonly Dictionary<string, TemplateFeatures> TemplateCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> TemplateCacheOrder = new();
    private static readonly object CacheLock = new();

    public PlanarMatchingOperator(ILogger<PlanarMatchingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 获取参数
        if (!TryNormalizeDetectorType(GetStringParam(@operator, "DetectorType", "ORB"), out var detectorType))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DetectorType must be ORB, AKAZE, or BRISK."));
        }

        var maxFeatures = GetIntParam(@operator, "MaxFeatures", 1000, 100, 5000);
        var scaleFactor = GetDoubleParam(@operator, "ScaleFactor", 1.2, 1.01, 2.0);
        var nLevels = GetIntParam(@operator, "NLevels", 8, 1, 16);
        var matchRatio = GetDoubleParam(@operator, "MatchRatio", 0.75, 0.5, 0.95);
        var ransacThreshold = GetDoubleParam(@operator, "RansacThreshold", 3.0, 0.5, 10.0);
        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10, 4, 100);
        var minInliers = GetIntParam(@operator, "MinInliers", Math.Min(8, minMatchCount), 4, 100);
        var minInlierRatio = GetDoubleParam(@operator, "MinInlierRatio", 0.25, 0.1, 1.0);
        var scoreThreshold = GetDoubleParam(@operator, "ScoreThreshold", 0.5, 0.0, 1.0);
        var useRoi = GetBoolParam(@operator, "UseRoi", false);
        var roiX = GetIntParam(@operator, "RoiX", 0);
        var roiY = GetIntParam(@operator, "RoiY", 0);
        var roiWidth = GetIntParam(@operator, "RoiWidth", 0);
        var roiHeight = GetIntParam(@operator, "RoiHeight", 0);
        var enableMultiScale = GetBoolParam(@operator, "EnableMultiScale", true);
        var scaleRange = GetDoubleParam(@operator, "ScaleRange", 0.2, 0.0, 1.0);
        var enableEarlyExit = GetBoolParam(@operator, "EnableEarlyExit", false);

        // 获取输入图像
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var searchImage = imageWrapper.GetMat();
        if (searchImage.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        // ROI处理
        Mat searchRoi = searchImage;
        Rect roi = new Rect(0, 0, searchImage.Width, searchImage.Height);
        bool needDisposeRoi = false;

        if (useRoi && roiWidth > 0 && roiHeight > 0)
        {
            roi = new Rect(roiX, roiY, roiWidth, roiHeight);
            roi = roi.Intersect(new Rect(0, 0, searchImage.Width, searchImage.Height));
            if (roi.Width > 0 && roi.Height > 0)
            {
                searchRoi = new Mat(searchImage, roi);
                needDisposeRoi = true;
            }
        }

        TemplateFeatures? templateFeatures = null;
        MatchResult? bestMatch = null;
        try
        {
            // 获取模板
            var templatePath = GetStringParam(@operator, "TemplatePath", "");
            if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
            {
                var templateMat = templateWrapper.GetMat();
                if (!templateMat.Empty())
                {
                    templateFeatures = ExtractFeatures(templateMat, detectorType, maxFeatures, scaleFactor, nLevels);
                }
            }
            else if (!string.IsNullOrEmpty(templatePath))
            {
                templateFeatures = GetOrLoadTemplateFeatures(templatePath, detectorType, maxFeatures, scaleFactor, nLevels);
            }

            var detectorDiagnostics = CreateDetectorDiagnostics(detectorType, maxFeatures, scaleFactor, nLevels);

            if (templateFeatures == null || templateFeatures.KeyPoints.Length < minMatchCount)
            {
                return Task.FromResult(CreateFailureOutput(
                    searchImage,
                    $"FeatureHomography:{detectorType}",
                    "Template features not available or insufficient.",
                    detectorDiagnostics));
            }

            // 多尺度搜索
            var scales = BuildScaleCandidates(enableMultiScale, scaleRange);

            foreach (var scale in scales)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MatchResult? match;
                if (Math.Abs(scale - 1.0) < 0.01)
                {
                    match = PerformMatching(searchRoi, templateFeatures, detectorType, maxFeatures, scaleFactor, nLevels,
                        matchRatio, ransacThreshold, minMatchCount, minInliers, minInlierRatio);
                }
                else
                {
                    // 缩放搜索图像
                    using var scaledImage = new Mat();
                    Cv2.Resize(searchRoi, scaledImage, new Size(0, 0), 1.0 / scale, 1.0 / scale, InterpolationFlags.Linear);
                    match = PerformMatching(scaledImage, templateFeatures, detectorType, maxFeatures, scaleFactor, nLevels,
                        matchRatio, ransacThreshold, minMatchCount, minInliers, minInlierRatio);

                    if (match != null)
                    {
                        // 调整角点
                        match.ScaleToOriginal(scale);
                    }
                }

                if (match == null)
                {
                    continue;
                }

                if (bestMatch == null || MatchResultComparer.Instance.Compare(match, bestMatch) > 0)
                {
                    bestMatch?.Dispose();
                    bestMatch = match;
                }
                else
                {
                    match.Dispose();
                }

                if (enableEarlyExit && bestMatch.VerificationPassed && bestMatch.Score >= scoreThreshold)
                {
                    break;
                }
            }

            stopwatch.Stop();
            var processingTime = stopwatch.ElapsedMilliseconds;

            if (bestMatch != null && (roi.X != 0 || roi.Y != 0))
            {
                bestMatch.OffsetToGlobal(roi);
            }

            // 创建结果
            if (bestMatch != null && bestMatch.VerificationPassed && bestMatch.Score >= scoreThreshold)
            {
                // 调整坐标到原图（考虑ROI）
                return Task.FromResult(CreateSuccessOutput(searchImage, bestMatch, processingTime, detectorDiagnostics));
            }
            var failureReason = bestMatch?.FailureReason ?? "No valid match found.";
            return Task.FromResult(CreateDetailedFailureOutput(searchImage, failureReason, bestMatch, processingTime, detectorDiagnostics));
        }
        finally
        {
            bestMatch?.Dispose();
            templateFeatures?.Dispose();
            if (needDisposeRoi)
            {
                searchRoi.Dispose();
            }
        }
    }

    private static IReadOnlyList<double> BuildScaleCandidates(bool enableMultiScale, double scaleRange)
    {
        if (!enableMultiScale || scaleRange <= 0)
        {
            return new[] { 1.0 };
        }

        return new[]
            {
                1.0,
                1.0 - (scaleRange / 2.0),
                1.0 + (scaleRange / 2.0),
                1.0 - scaleRange,
                1.0 + scaleRange
            }
            .Where(scale => scale > 0.1)
            .Distinct()
            .ToArray();
    }

    private static bool TryNormalizeDetectorType(string? detectorType, out string normalizedDetectorType)
    {
        if (string.IsNullOrWhiteSpace(detectorType))
        {
            normalizedDetectorType = "ORB";
            return true;
        }

        normalizedDetectorType = detectorType.Trim().ToUpperInvariant();
        return SupportedDetectorTypes.Contains(normalizedDetectorType, StringComparer.Ordinal);
    }

    private MatchResult? PerformMatching(Mat searchImage, TemplateFeatures templateFeatures, string detectorType,
        int maxFeatures, double scaleFactor, int nLevels, double matchRatio, double ransacThreshold,
        int minMatchCount, int minInliers, double minInlierRatio)
    {
        // 提取搜索图像特征
        var method = $"FeatureHomography:{detectorType}";
        using var searchFeatures = ExtractFeatures(searchImage, detectorType, maxFeatures, scaleFactor, nLevels, includeTemplateImage: false);
        if (searchFeatures == null || searchFeatures.KeyPoints.Length < minMatchCount)
        {
            return new MatchResult
            {
                Method = method,
                FailureReason = "Insufficient features in search image.",
                SearchFeatures = searchFeatures?.KeyPoints.Length ?? 0
            };
        }

        // 特征匹配
        var matches = MatchFeatures(templateFeatures.Descriptors, searchFeatures.Descriptors, detectorType, matchRatio);
        var candidateScore = CalculateCandidateScore(matches, detectorType, templateFeatures.KeyPoints.Length, searchFeatures.KeyPoints.Length);
        if (matches.Count < minMatchCount)
        {
            return new MatchResult
            {
                Method = method,
                FeatureMatchCount = matches.Count,
                CandidateScore = candidateScore,
                Score = candidateScore,
                FailureReason = $"Insufficient feature matches ({matches.Count} < {minMatchCount}).",
                TemplateFeatures = templateFeatures.KeyPoints.Length,
                SearchFeatures = searchFeatures.KeyPoints.Length
            };
        }

        // 提取匹配点
        var templatePoints = matches.Select(m => templateFeatures.KeyPoints[m.QueryIdx].Pt).ToArray();
        var searchPoints = matches.Select(m => searchFeatures.KeyPoints[m.TrainIdx].Pt).ToArray();

        // RANSAC单应性估计
        var verified = HomographyVerificationHelper.TryEstimateAndVerify(
            templatePoints,
            searchPoints,
            new Size(templateFeatures.ImageWidth, templateFeatures.ImageHeight),
            searchImage.Size(),
            ransacThreshold,
            minMatchCount,
            minInliers,
            minInlierRatio,
            out var homography,
            out var corners,
            out var verificationMetrics);

        // OpenCvSharp4 当前运行时的 FindHomography 输出掩码兼容性较差，这里先将通过 RANSAC
        // 建模的匹配视为候选内点，以保证算子链路稳定可用。
        var verificationScore = HomographyVerificationHelper.ComputeVerificationScore(verificationMetrics, ransacThreshold);
        var finalScore = Math.Clamp((candidateScore * 0.35) + (verificationScore * 0.65), 0, 1);

        // 计算透视变换后的角点
        var center = corners.Length == 4
            ? new Position(corners.Average(point => point.X), corners.Average(point => point.Y))
            : new Position(0, 0);

        // 计算分数
        return new MatchResult
        {
            Method = method,
            Homography = homography ?? new Mat(),
            Corners = corners,
            Center = center,
            VerificationPassed = verified,
            MatchCount = verified ? 1 : 0,
            FeatureMatchCount = matches.Count,
            InlierCount = verificationMetrics.InlierCount,
            InlierRatio = verificationMetrics.InlierRatio,
            CandidateScore = candidateScore,
            VerificationScore = verificationScore,
            Score = finalScore,
            MeanReprojectionError = verificationMetrics.MeanReprojectionError,
            AreaRatio = verificationMetrics.AreaRatio,
            FailureReason = verificationMetrics.FailureReason,
            TemplateFeatures = templateFeatures.KeyPoints.Length,
            SearchFeatures = searchFeatures.KeyPoints.Length
        };
    }

    private static double CalculateCandidateScore(
        IReadOnlyCollection<DMatch> matches,
        string detectorType,
        int templateFeatureCount,
        int searchFeatureCount)
    {
        if (matches.Count == 0)
        {
            return 0;
        }

        var averageDistance = matches.Average(match => match.Distance);
        var maxDistance = detectorType switch
        {
            "ORB" or "AKAZE" or "BRISK" => 256.0,
            _ => 512.0
        };
        var distanceScore = 1.0 - Math.Clamp(averageDistance / maxDistance, 0, 1);
        var coverageBase = Math.Max(1, Math.Min(templateFeatureCount, searchFeatureCount));
        var coverageScore = Math.Clamp(matches.Count / (double)coverageBase, 0, 1);
        return Math.Clamp((coverageScore * 0.65) + (distanceScore * 0.35), 0, 1);
        #if false

        // 综合评分：内点数、内点率、匹配率的加权组合
        var inlierCountScore = Math.Min(inlierCount / 50.0, 1.0); // 50个内点得满分
        var inlierRatioScore = inlierRatio;
        var matchRateScore = Math.Min(totalMatches / (double)templateFeatures, 1.0);

        return inlierCountScore * 0.4 + inlierRatioScore * 0.4 + matchRateScore * 0.2;
        #endif
    }

    private TemplateFeatures? ExtractFeatures(
        Mat image,
        string detectorType,
        int maxFeatures,
        double scaleFactor,
        int nLevels,
        bool includeTemplateImage = true)
    {
        try
        {
            using var gray = image.Channels() == 1 ? image.Clone() : new Mat();
            if (image.Channels() > 1)
            {
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            }

            KeyPoint[] keypoints;
            Mat descriptors;

            switch (detectorType)
            {
                case "AKAZE":
                    descriptors = new Mat();
                    using (var detector = AKAZE.Create())
                    {
                        detector.DetectAndCompute(gray, null, out keypoints, descriptors);
                    }
                    break;

                case "BRISK":
                    descriptors = new Mat();
                    using (var detector = BRISK.Create())
                    {
                        detector.DetectAndCompute(gray, null, out keypoints, descriptors);
                    }
                    break;

                case "ORB":
                    descriptors = new Mat();
                    using (var detector = ORB.Create(maxFeatures, (float)scaleFactor, nLevels))
                    {
                        detector.DetectAndCompute(gray, null, out keypoints, descriptors);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(detectorType), detectorType, "Unsupported detector type.");
            }

            if (keypoints.Length == 0 || descriptors.Empty())
            {
                descriptors?.Dispose();
                return null;
            }

            if (keypoints.Length > maxFeatures)
            {
                var (filteredKeypoints, filteredDescriptors) = FilterTopFeatures(keypoints, descriptors, maxFeatures);
                descriptors.Dispose();
                keypoints = filteredKeypoints;
                descriptors = filteredDescriptors;
            }

            return new TemplateFeatures
            {
                KeyPoints = keypoints,
                Descriptors = descriptors,
                TemplateImage = includeTemplateImage ? image.Clone() : new Mat(),
                ImageWidth = image.Width,
                ImageHeight = image.Height
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Feature extraction failed");
            return null;
        }
    }

    private static (KeyPoint[] KeyPoints, Mat Descriptors) FilterTopFeatures(KeyPoint[] keypoints, Mat descriptors, int maxFeatures)
    {
        var selectedIndices = Enumerable.Range(0, keypoints.Length)
            .OrderByDescending(index => keypoints[index].Response)
            .Take(maxFeatures)
            .ToArray();

        var filteredKeypoints = new KeyPoint[selectedIndices.Length];
        var filteredDescriptors = new Mat(selectedIndices.Length, descriptors.Cols, descriptors.Type());
        for (var outputIndex = 0; outputIndex < selectedIndices.Length; outputIndex++)
        {
            var inputIndex = selectedIndices[outputIndex];
            filteredKeypoints[outputIndex] = keypoints[inputIndex];
            using var srcRow = descriptors.Row(inputIndex);
            using var dstRow = filteredDescriptors.Row(outputIndex);
            srcRow.CopyTo(dstRow);
        }

        return (filteredKeypoints, filteredDescriptors);
    }

    private List<DMatch> MatchFeatures(Mat templateDescriptors, Mat searchDescriptors, string detectorType, double matchRatio)
    {
        var matches = new List<DMatch>();

        if (templateDescriptors.Empty() || searchDescriptors.Empty())
        {
            return matches;
        }

        try
        {
            // 选择距离类型
            var normType = detectorType.ToUpperInvariant() switch
            {
                "ORB" or "BRISK" or "AKAZE" => NormTypes.Hamming,
                _ => NormTypes.L2
            };

            using var matcher = new BFMatcher(normType, crossCheck: false);
            var forward = matcher.KnnMatch(templateDescriptors, searchDescriptors, k: 2);
            var backward = matcher.KnnMatch(searchDescriptors, templateDescriptors, k: 2);

            var backwardBest = new Dictionary<int, int>();
            foreach (var candidate in backward)
            {
                if (candidate.Length >= 2 && candidate[0].Distance < matchRatio * candidate[1].Distance)
                {
                    backwardBest[candidate[0].QueryIdx] = candidate[0].TrainIdx;
                }
            }

            foreach (var candidate in forward)
            {
                if (candidate.Length < 2 || candidate[0].Distance >= matchRatio * candidate[1].Distance)
                {
                    continue;
                }

                if (backwardBest.TryGetValue(candidate[0].TrainIdx, out var reverseTemplateIndex) &&
                    reverseTemplateIndex == candidate[0].QueryIdx)
                {
                    matches.Add(candidate[0]);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Feature matching failed");
        }

        return matches
            .OrderBy(match => match.Distance)
            .Take(300)
            .ToList();
    }

    private TemplateFeatures? GetOrLoadTemplateFeatures(string templatePath, string detectorType, int maxFeatures, double scaleFactor, int nLevels)
    {
        var cacheKey = BuildTemplateCacheKey(templatePath, detectorType, maxFeatures, scaleFactor, nLevels);

        lock (CacheLock)
        {
            if (TemplateCache.TryGetValue(cacheKey, out var cached))
            {
                TouchTemplateCacheEntry(cacheKey, cached);
                Logger.LogDebug("Template cache hit: {CacheKey}", cacheKey);
                return cached.Clone();
            }
        }

        if (!File.Exists(templatePath))
        {
            Logger.LogWarning("Template file not found: {Path}", templatePath);
            return null;
        }

        try
        {
            using var templateImage = Cv2.ImRead(templatePath, ImreadModes.Color);
            if (templateImage.Empty())
            {
                Logger.LogWarning("Failed to load template image: {Path}", templatePath);
                return null;
            }

            var features = ExtractFeatures(templateImage, detectorType, maxFeatures, scaleFactor, nLevels);
            if (features == null)
            {
                return null;
            }

            lock (CacheLock)
            {
                if (TemplateCache.TryGetValue(cacheKey, out var existing))
                {
                    features.Dispose();
                    TouchTemplateCacheEntry(cacheKey, existing);
                    return existing.Clone();
                }
                    // 限制缓存大小
                    while (TemplateCache.Count >= TemplateCacheCapacity)
                    {
                        var oldestKey = TemplateCacheOrder.First?.Value;
                        if (oldestKey == null)
                        {
                            break;
                        }

                        if (TemplateCache.Remove(oldestKey, out var evicted))
                        {
                            TemplateCacheOrder.RemoveFirst();
                            evicted.Dispose();
                        }
                    }

                    features.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
                    TemplateCache[cacheKey] = features;
                }

            return features.Clone();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load template features from {Path}", templatePath);
            return null;
        }
    }

    private OperatorExecutionOutput CreateSuccessOutput(
        Mat image,
        MatchResult match,
        long processingTime,
        IReadOnlyDictionary<string, object> detectorDiagnostics)
    {
        var resultImage = image.Clone();

        // 绘制检测框
        var corners = match.Corners;
        for (int i = 0; i < 4; i++)
        {
            var pt1 = new Point((int)corners[i].X, (int)corners[i].Y);
            var pt2 = new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y);
            Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 3);
        }

        // 绘制中心点
        var center = new Point((int)corners.Average(c => c.X), (int)corners.Average(c => c.Y));
        Cv2.Circle(resultImage, center, 5, new Scalar(0, 0, 255), -1);

        // 绘制信息
        Cv2.PutText(resultImage, $"Score: {match.Score:F3}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        Cv2.PutText(resultImage, $"Inliers: {match.InlierCount}/{match.FeatureMatchCount}", new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        Cv2.PutText(resultImage, $"Time: {processingTime}ms", new Point(10, 90),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var resultData = new Dictionary<string, object>
        {
            { "IsMatch", true },
            { "Score", match.Score },
            { "MatchCount", 1 },
            { "Method", match.Method },
            { "FailureReason", string.Empty },
            { "CandidateScore", match.CandidateScore },
            { "VerificationScore", match.VerificationScore },
            { "InlierCount", match.InlierCount },
            { "InlierRatio", match.InlierRatio },
            { "VerificationPassed", true },
            { "FeatureMatchCount", match.FeatureMatchCount },
            { "TemplateFeatures", match.TemplateFeatures },
            { "SearchFeatures", match.SearchFeatures },
            { "MeanReprojectionError", match.MeanReprojectionError },
            { "AreaRatio", match.AreaRatio },
            { "ProcessingTimeMs", processingTime },
            { "Center", match.Center },
            { "Corners", corners.Select(c => new Position(c.X, c.Y)).ToList() },
            { "Homography", match.Homography.Empty() ? new Mat() : match.Homography.Clone() },
            { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) },
            { "MatchResult", new Dictionary<string, object>
                {
                    { "Method", match.Method },
                    { "CandidateScore", match.CandidateScore },
                    { "VerificationScore", match.VerificationScore },
                    { "InlierCount", match.InlierCount },
                    { "InlierRatio", match.InlierRatio },
                    { "VerificationPassed", true },
                    { "FailureReason", string.Empty },
                    { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) }
                }
            },
            { "Message", "Match successful" }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, resultData));
    }

    private OperatorExecutionOutput CreateFailureOutput(
        Mat image,
        string method,
        string reason,
        IReadOnlyDictionary<string, object> detectorDiagnostics)
    {
        var resultImage = image.Clone();
        Cv2.PutText(resultImage, $"NG: {reason}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "Score", 0.0 },
            { "MatchCount", 0 },
            { "Method", method },
            { "FailureReason", reason },
            { "CandidateScore", 0.0 },
            { "VerificationScore", 0.0 },
            { "InlierCount", 0 },
            { "InlierRatio", 0.0 },
            { "VerificationPassed", false },
            { "FeatureMatchCount", 0 },
            { "TemplateFeatures", 0 },
            { "SearchFeatures", 0 },
            { "MeanReprojectionError", double.PositiveInfinity },
            { "AreaRatio", 0.0 },
            { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) },
            { "MatchResult", new Dictionary<string, object>
                {
                    { "Method", method },
                    { "CandidateScore", 0.0 },
                    { "VerificationScore", 0.0 },
                    { "InlierCount", 0 },
                    { "InlierRatio", 0.0 },
                    { "VerificationPassed", false },
                    { "FailureReason", reason },
                    { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) }
                }
            },
            { "Message", reason }
        }));
    }

    private OperatorExecutionOutput CreateDetailedFailureOutput(
        Mat image,
        string reason,
        MatchResult? match,
        long processingTime,
        IReadOnlyDictionary<string, object> detectorDiagnostics)
    {
        var resultImage = image.Clone();
        Cv2.PutText(resultImage, $"NG: {reason}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        if (match != null)
        {
            Cv2.PutText(resultImage, $"Score: {match.Score:F3}", new Point(10, 60),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 1);
            Cv2.PutText(resultImage, $"Inliers: {match.InlierCount}/{match.FeatureMatchCount}", new Point(10, 85),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 1);
        }

        var resultData = new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "Method", match?.Method ?? "FeatureHomography" },
            { "FailureReason", reason },
            { "ProcessingTimeMs", processingTime },
            { "MatchCount", match?.MatchCount ?? 0 },
            { "CandidateScore", match?.CandidateScore ?? 0.0 },
            { "VerificationScore", match?.VerificationScore ?? 0.0 },
            { "InlierCount", match?.InlierCount ?? 0 },
            { "InlierRatio", match?.InlierRatio ?? 0.0 },
            { "VerificationPassed", false },
            { "FeatureMatchCount", match?.FeatureMatchCount ?? 0 },
            { "Score", match?.Score ?? 0.0 },
            { "MeanReprojectionError", match?.MeanReprojectionError ?? double.PositiveInfinity },
            { "AreaRatio", match?.AreaRatio ?? 0.0 },
            { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) },
            { "MatchResult", new Dictionary<string, object>
                {
                    { "Method", match?.Method ?? "FeatureHomography" },
                    { "CandidateScore", match?.CandidateScore ?? 0.0 },
                    { "VerificationScore", match?.VerificationScore ?? 0.0 },
                    { "InlierCount", match?.InlierCount ?? 0 },
                    { "InlierRatio", match?.InlierRatio ?? 0.0 },
                    { "VerificationPassed", false },
                    { "FailureReason", reason },
                    { "DetectorParameterDiagnostics", new Dictionary<string, object>(detectorDiagnostics) }
                }
            },
            { "Message", reason }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, resultData));
    }

    private static string BuildTemplateCacheKey(string templatePath, string detectorType, int maxFeatures, double scaleFactor, int nLevels)
    {
        var fingerprint = ComputeFileFingerprint(templatePath);
        return $"{templatePath}|{fingerprint}|{detectorType}|{maxFeatures}|{scaleFactor:F3}|{nLevels}";
    }

    private static string ComputeFileFingerprint(string templatePath)
    {
        using var stream = File.OpenRead(templatePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static void TouchTemplateCacheEntry(string cacheKey, TemplateFeatures entry)
    {
        if (entry.OrderNode != null)
        {
            TemplateCacheOrder.Remove(entry.OrderNode);
        }

        entry.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
    }

    private static Dictionary<string, object> CreateDetectorDiagnostics(string detectorType, int maxFeatures, double scaleFactor, int nLevels)
    {
        var isOrb = string.Equals(detectorType, "ORB", StringComparison.Ordinal);
        return new Dictionary<string, object>
        {
            { "DetectorType", detectorType },
            { "MaxFeaturesApplied", true },
            { "ScaleFactorApplied", isOrb },
            { "NLevelsApplied", isOrb },
            { "Notes", isOrb
                ? "ORB applies MaxFeatures, ScaleFactor, and NLevels."
                : "MaxFeatures is applied by response-based post-filtering. ScaleFactor and NLevels remain ORB-only in phase 1." }
        };
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        if (!TryNormalizeDetectorType(GetStringParam(@operator, "DetectorType", "ORB"), out _))
        {
            return ValidationResult.Invalid("DetectorType must be ORB, AKAZE, or BRISK.");
        }

        var matchRatio = GetDoubleParam(@operator, "MatchRatio", 0.75);
        if (matchRatio < 0.5 || matchRatio > 0.95)
        {
            return ValidationResult.Invalid("MatchRatio must be between 0.5 and 0.95.");
        }

        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10);
        if (minMatchCount < 4)
        {
            return ValidationResult.Invalid("MinMatchCount must be at least 4.");
        }

        var minInliers = GetIntParam(@operator, "MinInliers", 8);
        if (minInliers < 4)
        {
            return ValidationResult.Invalid("MinInliers must be at least 4.");
        }

        var roiWidth = GetIntParam(@operator, "RoiWidth", 0);
        var roiHeight = GetIntParam(@operator, "RoiHeight", 0);
        if (roiWidth < 0 || roiHeight < 0)
        {
            return ValidationResult.Invalid("ROI dimensions must be non-negative.");
        }

        return ValidationResult.Valid();
    }

    private sealed class TemplateFeatures : IDisposable
    {
        public KeyPoint[] KeyPoints { get; set; } = Array.Empty<KeyPoint>();
        public Mat Descriptors { get; set; } = new Mat();
        public Mat TemplateImage { get; set; } = new Mat();
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public LinkedListNode<string>? OrderNode { get; set; }

        public TemplateFeatures Clone()
        {
            return new TemplateFeatures
            {
                KeyPoints = KeyPoints.ToArray(),
                Descriptors = Descriptors.Clone(),
                TemplateImage = TemplateImage.Empty() ? new Mat() : TemplateImage.Clone(),
                ImageWidth = ImageWidth,
                ImageHeight = ImageHeight
            };
        }

        public void Dispose()
        {
            Descriptors.Dispose();
            TemplateImage.Dispose();
        }
    }

    private sealed class MatchResult : IDisposable
    {
        public bool IsSuccess { get; set; }
        public string Method { get; set; } = "FeatureHomography:ORB";
        public string FailureReason { get; set; } = string.Empty;
        public Mat Homography { get; set; } = new Mat();
        public Point2f[] Corners { get; set; } = Array.Empty<Point2f>();
        public Position Center { get; set; } = new(0, 0);
        public bool VerificationPassed { get; set; }
        public int MatchCount { get; set; }
        public int FeatureMatchCount { get; set; }
        public int InlierCount { get; set; }
        public double InlierRatio { get; set; }
        public double CandidateScore { get; set; }
        public double VerificationScore { get; set; }
        public double Score { get; set; }
        public double MeanReprojectionError { get; set; }
        public double AreaRatio { get; set; }
        public int TemplateFeatures { get; set; }
        public int SearchFeatures { get; set; }

        public void ScaleToOriginal(double scale)
        {
            Corners = Corners
                .Select(point => new Point2f((float)(point.X * scale), (float)(point.Y * scale)))
                .ToArray();
            Center = new Position(Center.X * scale, Center.Y * scale);
            LeftMultiplyHomography(new[,]
            {
                { scale, 0.0, 0.0 },
                { 0.0, scale, 0.0 },
                { 0.0, 0.0, 1.0 }
            });
        }

        public void OffsetToGlobal(Rect roi)
        {
            Corners = Corners
                .Select(point => new Point2f(point.X + roi.X, point.Y + roi.Y))
                .ToArray();
            Center = new Position(Center.X + roi.X, Center.Y + roi.Y);
            LeftMultiplyHomography(new[,]
            {
                { 1.0, 0.0, roi.X },
                { 0.0, 1.0, roi.Y },
                { 0.0, 0.0, 1.0 }
            });
        }

        private void LeftMultiplyHomography(double[,] lhs)
        {
            if (Homography.Empty())
            {
                return;
            }

            var values = new double[3, 3];
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    values[row, col] = Homography.Get<double>(row, col);
                }
            }

            var multiplied = new double[3, 3];
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    multiplied[row, col] =
                        (lhs[row, 0] * values[0, col]) +
                        (lhs[row, 1] * values[1, col]) +
                        (lhs[row, 2] * values[2, col]);
                }
            }

            var adjusted = new Mat(3, 3, MatType.CV_64FC1);
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    adjusted.Set(row, col, multiplied[row, col]);
                }
            }

            Homography.Dispose();
            Homography = adjusted;
        }

        public void Dispose()
        {
            Homography.Dispose();
        }
    }

    private sealed class MatchResultComparer : IComparer<MatchResult>
    {
        public static MatchResultComparer Instance { get; } = new();

        public int Compare(MatchResult? x, MatchResult? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var verificationCompare = x.VerificationPassed.CompareTo(y.VerificationPassed);
            if (verificationCompare != 0)
            {
                return verificationCompare;
            }

            var scoreCompare = x.Score.CompareTo(y.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var verificationScoreCompare = x.VerificationScore.CompareTo(y.VerificationScore);
            if (verificationScoreCompare != 0)
            {
                return verificationScoreCompare;
            }

            return x.CandidateScore.CompareTo(y.CandidateScore);
        }
    }

    private MatchResult? TryCorrelationFallback(Mat searchImage, TemplateFeatures templateFeatures, double scoreThreshold)
    {
        if (templateFeatures.TemplateImage.Empty() ||
            searchImage.Width < templateFeatures.TemplateImage.Width ||
            searchImage.Height < templateFeatures.TemplateImage.Height)
        {
            return null;
        }

        using var graySearch = searchImage.Channels() == 1 ? searchImage.Clone() : searchImage.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var grayTemplate = templateFeatures.TemplateImage.Channels() == 1 ? templateFeatures.TemplateImage.Clone() : templateFeatures.TemplateImage.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var response = new Mat();

        Cv2.MatchTemplate(graySearch, grayTemplate, response, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(response, out _, out var maxVal, out _, out var maxLoc);

        if (maxVal < Math.Max(0.4, scoreThreshold * 0.8))
        {
            return null;
        }

        var corners = new[]
        {
            new Point2f(maxLoc.X, maxLoc.Y),
            new Point2f(maxLoc.X + templateFeatures.ImageWidth, maxLoc.Y),
            new Point2f(maxLoc.X + templateFeatures.ImageWidth, maxLoc.Y + templateFeatures.ImageHeight),
            new Point2f(maxLoc.X, maxLoc.Y + templateFeatures.ImageHeight)
        };

        var homography = new Mat(3, 3, MatType.CV_64FC1, Scalar.All(0));
        homography.Set(0, 0, 1.0);
        homography.Set(1, 1, 1.0);
        homography.Set(2, 2, 1.0);
        homography.Set(0, 2, maxLoc.X);
        homography.Set(1, 2, maxLoc.Y);

        return new MatchResult
        {
            IsSuccess = true,
            Homography = homography,
            Corners = corners,
            MatchCount = 1,
            InlierCount = 1,
            InlierRatio = 1.0,
            Score = maxVal,
            TemplateFeatures = templateFeatures.KeyPoints.Length,
            SearchFeatures = 0
        };
    }
}
