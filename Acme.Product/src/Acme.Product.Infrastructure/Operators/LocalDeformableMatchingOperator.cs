// LocalDeformableMatchingOperator.cs
// 局部可变形匹配算子 (实验级 MVP)
// 对标 Halcon: find_local_deformable_model
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 局部可变形匹配算子 - 支持局部形变和遮挡的模板匹配
/// 对标 Halcon find_local_deformable_model
/// Phase 4.2: 扩展为多目标候选、NMS 去重和批量变形评估
/// </summary>
[OperatorMeta(
    DisplayName = "Local Deformable Matching",
    Description = "Local deformable matching with TPS deformation field estimation, multi-candidate search and NMS-based multi-target output.",
    Category = "Matching",
    IconName = "deformable-match",
    Keywords = new[] { "Deformable", "Local", "Matching", "TPS", "Occlusion", "MultiTarget", "NMS" }
)]
[InputPort("Image", "Search Image", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "Template Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("MatchResult", "Match Result", PortDataType.Any)]
[OutputPort("Matches", "Match List", PortDataType.Any)]
[OutputPort("MatchCount", "Match Count", PortDataType.Integer)]
[OutputPort("DeformationField", "Deformation Field", PortDataType.Any)]
[OutputPort("OcclusionMask", "Occlusion Mask", PortDataType.Image)]
[OperatorParam("TemplatePath", "Template Image Path", "file", DefaultValue = "")]
[OperatorParam("PyramidLevels", "Pyramid Levels", "int", DefaultValue = 3, Min = 1, Max = 6)]
[OperatorParam("TPSGridSize", "TPS Grid Size", "int", DefaultValue = 4, Min = 2, Max = 8)]
[OperatorParam("TPSLambda", "TPS Regularization", "double", DefaultValue = 0.01, Min = 0.001, Max = 1.0)]
[OperatorParam("MaxDeformation", "Max Deformation (px)", "double", DefaultValue = 20.0, Min = 5.0, Max = 100.0)]
[OperatorParam("OcclusionThreshold", "Occlusion Threshold", "double", DefaultValue = 0.3, Min = 0.1, Max = 0.9)]
[OperatorParam("MinMatchScore", "Min Match Score", "double", DefaultValue = 0.6, Min = 0.0, Max = 1.0)]
[OperatorParam("EnableFallback", "Enable Fallback to Rigid", "bool", DefaultValue = true)]
[OperatorParam("MaxIterations", "Max Refinement Iterations", "int", DefaultValue = 5, Min = 1, Max = 20)]
[OperatorParam("ConvergenceThreshold", "Convergence Threshold", "double", DefaultValue = 0.5, Min = 0.1, Max = 5.0)]
[OperatorParam("MaxMatches", "Maximum Matches", "int", DefaultValue = 5, Min = 1, Max = 20)]
[OperatorParam("CandidateThreshold", "Candidate Seed Threshold", "double", DefaultValue = 0.65, Min = 0.1, Max = 1.0)]
[OperatorParam("EnableNms", "Enable NMS", "bool", DefaultValue = true)]
[OperatorParam("NmsThreshold", "NMS IoU Threshold", "double", DefaultValue = 0.35, Min = 0.0, Max = 1.0)]
[OperatorParam("ParallelCandidates", "Parallel Candidate Evaluation", "bool", DefaultValue = true)]
public class LocalDeformableMatchingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LocalDeformableMatching;

    // 模板缓存
    private static readonly Dictionary<string, TemplateData> TemplateCache = new();
    private static readonly object CacheLock = new();

    public LocalDeformableMatchingOperator(ILogger<LocalDeformableMatchingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 参数
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, 1, 6);
        var tpsGridSize = GetIntParam(@operator, "TPSGridSize", 4, 2, 8);
        var tpsLambda = GetDoubleParam(@operator, "TPSLambda", 0.01, 0.001, 1.0);
        var maxDeformation = GetDoubleParam(@operator, "MaxDeformation", 20.0, 5.0, 100.0);
        var occlusionThreshold = GetDoubleParam(@operator, "OcclusionThreshold", 0.3, 0.1, 0.9);
        var minMatchScore = GetDoubleParam(@operator, "MinMatchScore", 0.6, 0.0, 1.0);
        var enableFallback = GetBoolParam(@operator, "EnableFallback", true);
        var maxIterations = GetIntParam(@operator, "MaxIterations", 5, 1, 20);
        var convergenceThreshold = GetDoubleParam(@operator, "ConvergenceThreshold", 0.5, 0.1, 5.0);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 5, 1, 20);
        var candidateThreshold = GetDoubleParam(@operator, "CandidateThreshold", 0.65, 0.1, 1.0);
        var enableNms = GetBoolParam(@operator, "EnableNms", true);
        var nmsThreshold = GetDoubleParam(@operator, "NmsThreshold", 0.35, 0.0, 1.0);
        var parallelCandidates = GetBoolParam(@operator, "ParallelCandidates", true);

        // 获取图像
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var searchImage = imageWrapper.GetMat();
        if (searchImage.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        // 获取模板
        TemplateData? template = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            var templateMat = templateWrapper.GetMat();
            if (!templateMat.Empty())
            {
                template = BuildTemplatePyramid(templateMat, pyramidLevels);
            }
        }
        else
        {
            var templatePath = GetStringParam(@operator, "TemplatePath", "");
            if (!string.IsNullOrEmpty(templatePath))
            {
                template = GetOrLoadTemplate(templatePath, pyramidLevels);
            }
        }

        if (template == null)
        {
            return Task.FromResult(CreateFailureOutput(searchImage, "Template not available", 0));
        }

        try
        {
            var candidates = GenerateCandidateWindows(searchImage, template.BaseImage, maxMatches, candidateThreshold, maxDeformation);
            var candidateResults = EvaluateCandidates(
                searchImage, template, candidates, parallelCandidates,
                pyramidLevels, tpsGridSize, tpsLambda, maxDeformation,
                occlusionThreshold, minMatchScore, enableFallback,
                maxIterations, convergenceThreshold, cancellationToken);

            var acceptedMatches = candidateResults
                .Where(result => result.IsSuccess && result.Score >= minMatchScore)
                .OrderByDescending(result => result.Score)
                .ToList();

            if (enableNms)
            {
                acceptedMatches = ApplyNms(acceptedMatches, nmsThreshold, maxMatches);
            }
            else
            {
                acceptedMatches = acceptedMatches.Take(maxMatches).ToList();
            }

            stopwatch.Stop();

            if (acceptedMatches.Count > 0)
            {
                return Task.FromResult(CreateSuccessOutput(searchImage, acceptedMatches, template, stopwatch.ElapsedMilliseconds));
            }

            if (enableFallback)
            {
                var seedFallbackMatches = candidates
                    .Where(candidate => candidate.Score >= candidateThreshold)
                    .Select(candidate => CreateSeedFallbackMatch(candidate, template))
                    .ToList();

                if (seedFallbackMatches.Count > 0)
                {
                    if (enableNms)
                    {
                        seedFallbackMatches = ApplyNms(seedFallbackMatches, nmsThreshold, maxMatches);
                    }
                    else
                    {
                        seedFallbackMatches = seedFallbackMatches.Take(maxMatches).ToList();
                    }

                    return Task.FromResult(CreateSuccessOutput(searchImage, seedFallbackMatches, template, stopwatch.ElapsedMilliseconds));
                }
            }

            var bestFallback = candidateResults
                .Where(result => result.IsFallback)
                .OrderByDescending(result => result.Score)
                .FirstOrDefault();

            if (enableFallback && bestFallback != null)
            {
                return Task.FromResult(CreateFallbackOutput(searchImage, bestFallback, template, stopwatch.ElapsedMilliseconds));
            }

            var failureResult = candidateResults
                .OrderByDescending(result => result.Score)
                .FirstOrDefault() ?? new DeformableMatchResult { FailureReason = "No valid candidate match found" };

            return Task.FromResult(CreateDetailedFailureOutput(searchImage, failureResult, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Deformable matching failed");
            return Task.FromResult(CreateFailureOutput(searchImage, $"Exception: {ex.Message}", 0));
        }
    }

    private DeformableMatchResult PerformCoarseToFineMatching(
        Mat searchImage, TemplateData template, int pyramidLevels, int tpsGridSize,
        double tpsLambda, double maxDeformation, double occlusionThreshold,
        double minMatchScore, int maxIterations, double convergenceThreshold,
        CancellationToken cancellationToken)
    {
        var result = new DeformableMatchResult();
        var currentLevel = pyramidLevels - 1;

        // 构建搜索图像金字塔
        var searchPyramid = BuildImagePyramid(searchImage, pyramidLevels);

        // 从粗到细迭代
        Mat? currentHomography = null;
        Point2f[]? controlPoints = null;
        Point2f[]? deformedPoints = null;

        for (int level = currentLevel; level >= 0; level--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var levelTemplate = template.Pyramid[level];
            var levelSearch = searchPyramid[level];

            // 缩放控制点到当前层级
            var scale = Math.Pow(2, level);
            if (controlPoints == null)
            {
                // 初始化均匀网格控制点
                controlPoints = InitializeControlPoints(
                    levelTemplate.Width, levelTemplate.Height, tpsGridSize);
            }

            // 特征匹配
            var (matches, templateKpts, searchKpts) = MatchFeaturesAtLevel(
                levelTemplate.Image, levelSearch, levelTemplate.KeyPoints, levelTemplate.Descriptors);

            if (matches.Count < 10)
            {
                result.FailureReason = $"Insufficient matches at level {level}: {matches.Count}";
                result.RigidFallbackResult = TryRigidFallback(
                    levelSearch, levelTemplate, matches, templateKpts, searchKpts);
                return result;
            }

            // 估计初始刚性变换（鲁棒估计）
            if (currentHomography == null)
            {
                currentHomography = EstimateRigidTransform(
                    matches, templateKpts, searchKpts, maxDeformation / scale);
                if (currentHomography == null)
                {
                    result.FailureReason = "Failed to estimate initial rigid transform";
                    return result;
                }
            }

            // TPS形变细化
            var iteration = 0;
            var converged = false;
            var prevError = double.MaxValue;

            while (iteration < maxIterations && !converged)
            {
                // 计算当前变换下的特征点对应
                var correspondences = ComputeCorrespondences(
                    matches, templateKpts, searchKpts, controlPoints, currentHomography);

                // 估计形变场
                deformedPoints = EstimateTPSDeformation(
                    controlPoints, correspondences, tpsLambda, maxDeformation / scale);

                // 应用形变并计算误差
                var (warpedImage, warpedMask) = ApplyTPSWarp(
                    levelTemplate.Image, controlPoints, deformedPoints, levelSearch.Size());

                // 计算匹配分数和遮挡
                var (score, occlusionMask, meanError) = ComputeMatchScoreAndOcclusion(
                    warpedImage, warpedMask, levelSearch, occlusionThreshold);

                // 检查收敛
                if (Math.Abs(prevError - meanError) < convergenceThreshold)
                {
                    converged = true;
                }
                prevError = meanError;

                // 更新控制点用于下一轮
                for (int i = 0; i < controlPoints.Length; i++)
                {
                    controlPoints[i] = new Point2f(
                        controlPoints[i].X + (float)(deformedPoints[i].X - controlPoints[i].X) * 0.5f,
                        controlPoints[i].Y + (float)(deformedPoints[i].Y - controlPoints[i].Y) * 0.5f);
                }

                iteration++;

                warpedImage.Dispose();
                warpedMask.Dispose();
            }

            // 上采样到下一层
            if (level > 0)
            {
                controlPoints = UpsampleControlPoints(controlPoints, 2.0);
            }
        }

        // 最终验证
        if (currentHomography != null && controlPoints != null && deformedPoints != null)
        {
            var (finalScore, finalOcclusionMask, finalDeformation) = ValidateFinalMatch(
                searchImage, template.BaseImage, controlPoints, deformedPoints, currentHomography);

            result.IsSuccess = finalScore >= minMatchScore;
            result.Score = finalScore;
            result.OcclusionRate = ComputeOcclusionRate(finalOcclusionMask);
            result.DeformationMagnitude = finalDeformation;
            result.ControlPoints = controlPoints;
            result.DeformedPoints = deformedPoints;
            result.Homography = currentHomography;
            result.OcclusionMask = finalOcclusionMask;

            if (!result.IsSuccess)
            {
                result.FailureReason = $"Final score {finalScore:F3} below threshold {minMatchScore}";
            }
        }
        else
        {
            result.FailureReason = "Failed to compute deformation field";
        }

        // 清理
        foreach (var level in searchPyramid)
        {
            level.Dispose();
        }

        return result;
    }

    private List<CandidateWindow> GenerateCandidateWindows(Mat searchImage, Mat templateImage, int maxMatches, double candidateThreshold, double maxDeformation)
    {
        var candidates = new List<CandidateWindow>();
        if (searchImage.Empty() || templateImage.Empty())
        {
            return candidates;
        }

        using var searchGray = searchImage.Channels() == 1 ? searchImage.Clone() : searchImage.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var templateGray = templateImage.Channels() == 1 ? templateImage.Clone() : templateImage.CvtColor(ColorConversionCodes.BGR2GRAY);

        if (searchGray.Width < templateGray.Width || searchGray.Height < templateGray.Height)
        {
            candidates.Add(new CandidateWindow(new Rect(0, 0, searchImage.Width, searchImage.Height), new Rect(0, 0, searchImage.Width, searchImage.Height), 1.0));
            return candidates;
        }

        using var response = new Mat();
        Cv2.MatchTemplate(searchGray, templateGray, response, TemplateMatchModes.CCoeffNormed);
        using var working = response.Clone();

        var padding = (int)Math.Ceiling(Math.Max(templateGray.Width, templateGray.Height) * 0.15 + maxDeformation);
        while (candidates.Count < maxMatches)
        {
            Cv2.MinMaxLoc(working, out _, out var maxVal, out _, out var maxLoc);
            if (maxVal < candidateThreshold)
            {
                break;
            }

            var center = new Point(
                maxLoc.X + templateGray.Width / 2,
                maxLoc.Y + templateGray.Height / 2);
            var proposal = new Rect(maxLoc.X, maxLoc.Y, templateGray.Width, templateGray.Height);
            var roi = BuildCandidateRoi(center, templateGray.Size(), searchImage.Size(), padding);
            candidates.Add(new CandidateWindow(roi, proposal, maxVal));

            var suppressX = Math.Max(0, maxLoc.X - templateGray.Width / 2);
            var suppressY = Math.Max(0, maxLoc.Y - templateGray.Height / 2);
            var suppressW = Math.Min(working.Width - suppressX, Math.Max(1, templateGray.Width));
            var suppressH = Math.Min(working.Height - suppressY, Math.Max(1, templateGray.Height));
            using var suppression = new Mat(working, new Rect(suppressX, suppressY, suppressW, suppressH));
            suppression.SetTo(Scalar.All(-1.0));
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new CandidateWindow(new Rect(0, 0, searchImage.Width, searchImage.Height), new Rect(0, 0, templateImage.Width, templateImage.Height), 0));
        }

        return candidates;
    }

    private List<DeformableMatchResult> EvaluateCandidates(
        Mat searchImage,
        TemplateData template,
        IReadOnlyList<CandidateWindow> candidates,
        bool parallelCandidates,
        int pyramidLevels,
        int tpsGridSize,
        double tpsLambda,
        double maxDeformation,
        double occlusionThreshold,
        double minMatchScore,
        bool enableFallback,
        int maxIterations,
        double convergenceThreshold,
        CancellationToken cancellationToken)
    {
        var results = new List<DeformableMatchResult>();
        var sync = new object();

        void Evaluate(CandidateWindow candidate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var localSearch = new Mat(searchImage, candidate.Roi);
            var localResult = PerformCoarseToFineMatching(
                localSearch, template, pyramidLevels, tpsGridSize, tpsLambda, maxDeformation,
                occlusionThreshold, minMatchScore, maxIterations, convergenceThreshold, cancellationToken);

            localResult.CandidateScore = candidate.Score;
            OffsetResultToGlobal(localResult, template, candidate.Roi);

            if (!localResult.IsSuccess && enableFallback && localResult.RigidFallbackResult != null)
            {
                PromoteFallback(localResult, candidate.Roi);
            }

            lock (sync)
            {
                results.Add(localResult);
            }
        }

        if (parallelCandidates && candidates.Count > 1)
        {
            Parallel.ForEach(candidates, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, candidates.Count)
            }, Evaluate);
        }
        else
        {
            foreach (var candidate in candidates)
            {
                Evaluate(candidate);
            }
        }

        return results;
    }

    private static Rect BuildCandidateRoi(Point center, Size templateSize, Size imageSize, int padding)
    {
        var x = Math.Max(0, center.X - templateSize.Width / 2 - padding);
        var y = Math.Max(0, center.Y - templateSize.Height / 2 - padding);
        var right = Math.Min(imageSize.Width, center.X + templateSize.Width / 2 + padding);
        var bottom = Math.Min(imageSize.Height, center.Y + templateSize.Height / 2 + padding);

        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    private static void OffsetResultToGlobal(DeformableMatchResult result, TemplateData template, Rect roi)
    {
        if (result.ControlPoints != null)
        {
            result.ControlPoints = result.ControlPoints
                .Select(point => new Point2f(point.X + roi.X, point.Y + roi.Y))
                .ToArray();
        }

        if (result.DeformedPoints != null)
        {
            result.DeformedPoints = result.DeformedPoints
                .Select(point => new Point2f(point.X + roi.X, point.Y + roi.Y))
                .ToArray();
        }

        if (result.Homography != null && !result.Homography.Empty())
        {
            var corners = new[]
            {
                new Point2f(0, 0),
                new Point2f(template.Width, 0),
                new Point2f(template.Width, template.Height),
                new Point2f(0, template.Height)
            };
            result.Corners = Cv2.PerspectiveTransform(corners, result.Homography)
                .Select(point => new Point2f(point.X + roi.X, point.Y + roi.Y))
                .ToArray();
        }

        if (result.Corners != null && result.Corners.Length > 0)
        {
            result.BoundingBox = ComputeBoundingBox(result.Corners);
        }
    }

    private static void PromoteFallback(DeformableMatchResult result, Rect roi)
    {
        var fallback = result.RigidFallbackResult;
        if (fallback == null)
        {
            return;
        }

        result.IsSuccess = true;
        result.IsFallback = true;
        result.Score = fallback.Score;
        result.Corners = fallback.Corners
            .Select(point => new Point2f(point.X + roi.X, point.Y + roi.Y))
            .ToArray();
        result.BoundingBox = ComputeBoundingBox(result.Corners);
        result.FailureReason = string.Empty;
    }

    private static List<DeformableMatchResult> ApplyNms(List<DeformableMatchResult> matches, double iouThreshold, int maxMatches)
    {
        var selected = new List<DeformableMatchResult>();
        foreach (var match in matches.OrderByDescending(m => m.Score))
        {
            var overlaps = selected.Any(existing => ComputeIoU(existing.BoundingBox, match.BoundingBox) > iouThreshold);
            if (!overlaps)
            {
                selected.Add(match);
            }

            if (selected.Count >= maxMatches)
            {
                break;
            }
        }

        return selected;
    }

    private static Rect ComputeBoundingBox(Point2f[] corners)
    {
        if (corners.Length == 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        var minX = (int)Math.Floor(corners.Min(point => point.X));
        var minY = (int)Math.Floor(corners.Min(point => point.Y));
        var maxX = (int)Math.Ceiling(corners.Max(point => point.X));
        var maxY = (int)Math.Ceiling(corners.Max(point => point.Y));
        return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private static double ComputeIoU(Rect a, Rect b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.Right, b.Right);
        var y2 = Math.Min(a.Bottom, b.Bottom);

        var intersectionWidth = Math.Max(0, x2 - x1);
        var intersectionHeight = Math.Max(0, y2 - y1);
        var intersection = intersectionWidth * intersectionHeight;
        if (intersection <= 0)
        {
            return 0;
        }

        var union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static DeformableMatchResult CreateSeedFallbackMatch(CandidateWindow candidate, TemplateData template)
    {
        var corners = new[]
        {
            new Point2f(candidate.Proposal.Left, candidate.Proposal.Top),
            new Point2f(candidate.Proposal.Right, candidate.Proposal.Top),
            new Point2f(candidate.Proposal.Right, candidate.Proposal.Bottom),
            new Point2f(candidate.Proposal.Left, candidate.Proposal.Bottom)
        };

        return new DeformableMatchResult
        {
            IsSuccess = true,
            IsFallback = true,
            Score = candidate.Score,
            CandidateScore = candidate.Score,
            Corners = corners,
            BoundingBox = candidate.Proposal,
            FailureReason = string.Empty
        };
    }

    private TemplateData BuildTemplatePyramid(Mat templateImage, int levels)
    {
        var data = new TemplateData
        {
            BaseImage = templateImage.Clone(),
            Width = templateImage.Width,
            Height = templateImage.Height,
            Pyramid = new List<PyramidLevel>()
        };

        var currentImage = templateImage.Clone();
        for (int i = 0; i < levels; i++)
        {
            using var gray = currentImage.Channels() == 1
                ? currentImage.Clone()
                : new Mat();
            if (currentImage.Channels() > 1)
            {
                Cv2.CvtColor(currentImage, gray, ColorConversionCodes.BGR2GRAY);
            }

            // 提取特征
            using var orb = ORB.Create(1000, 1.2f, 8);
            KeyPoint[] keypoints;
            Mat descriptors = new Mat();
            orb.DetectAndCompute(gray, null, out keypoints, descriptors);

            data.Pyramid.Add(new PyramidLevel
            {
                Image = currentImage.Clone(),
                KeyPoints = keypoints,
                Descriptors = descriptors,
                Width = currentImage.Width,
                Height = currentImage.Height,
                Scale = Math.Pow(2, i)
            });

            // 下采样
            if (i < levels - 1)
            {
                using var nextImage = new Mat();
                Cv2.PyrDown(currentImage, nextImage);
                currentImage = nextImage.Clone();
            }
        }

        currentImage.Dispose();
        return data;
    }

    private List<Mat> BuildImagePyramid(Mat image, int levels)
    {
        var pyramid = new List<Mat> { image.Clone() };
        var current = image;

        for (int i = 1; i < levels; i++)
        {
            using var next = new Mat();
            Cv2.PyrDown(current, next);
            pyramid.Add(next.Clone());
            current = pyramid.Last();
        }

        return pyramid;
    }

    private Point2f[] InitializeControlPoints(int width, int height, int gridSize)
    {
        var points = new List<Point2f>();
        var stepX = width / (gridSize - 1.0);
        var stepY = height / (gridSize - 1.0);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                var px = (float)(x * stepX);
                var py = (float)(y * stepY);
                // 边界处理
                if (x == gridSize - 1) px = width;
                if (y == gridSize - 1) py = height;
                points.Add(new Point2f(px, py));
            }
        }

        return points.ToArray();
    }

    private (List<DMatch> matches, KeyPoint[] templateKpts, KeyPoint[] searchKpts) MatchFeaturesAtLevel(
        Mat templateImage, Mat searchImage, KeyPoint[] templateKpts, Mat templateDesc)
    {
        // 提取搜索图像特征
        using var graySearch = searchImage.Channels() == 1
            ? searchImage.Clone()
            : new Mat();
        if (searchImage.Channels() > 1)
        {
            Cv2.CvtColor(searchImage, graySearch, ColorConversionCodes.BGR2GRAY);
        }

        using var orb = ORB.Create(1000, 1.2f, 8);
        KeyPoint[] searchKpts;
        Mat searchDesc = new Mat();
        orb.DetectAndCompute(graySearch, null, out searchKpts, searchDesc);

        if (templateDesc.Empty() || searchDesc.Empty())
        {
            searchDesc.Dispose();
            return (new List<DMatch>(), templateKpts, searchKpts);
        }

        // KNN匹配
        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
        var knnMatches = matcher.KnnMatch(templateDesc, searchDesc, k: 2);

        var goodMatches = new List<DMatch>();
        foreach (var match in knnMatches)
        {
            if (match.Length >= 2 && match[0].Distance < 0.75 * match[1].Distance)
            {
                goodMatches.Add(match[0]);
            }
        }

        if (goodMatches.Count < 4)
        {
            goodMatches.Clear();
            using var crossCheckMatcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
            goodMatches.AddRange(crossCheckMatcher.Match(templateDesc, searchDesc)
                .OrderBy(match => match.Distance)
                .Take(200));
        }

        searchDesc.Dispose();
        return (goodMatches, templateKpts, searchKpts);
    }

    private Mat? EstimateRigidTransform(List<DMatch> matches, KeyPoint[] templateKpts,
        KeyPoint[] searchKpts, double maxDeformation)
    {
        if (matches.Count < 4) return null;

        var srcPoints = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var dstPoints = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();

        using var homography = Cv2.FindHomography(
            InputArray.Create(srcPoints),
            InputArray.Create(dstPoints),
            HomographyMethods.Ransac,
            maxDeformation);

        return homography.Empty() ? null : homography.Clone();
    }

    private (Point2f[] templatePts, Point2f[] searchPts) ComputeCorrespondences(
        List<DMatch> matches, KeyPoint[] templateKpts, KeyPoint[] searchKpts,
        Point2f[] controlPoints, Mat homography)
    {
        // 根据单应性变换模板特征点
        var templatePts = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var warpedTemplatePts = Cv2.PerspectiveTransform(templatePts, homography);

        var searchPts = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();

        return (warpedTemplatePts, searchPts);
    }

    private Point2f[] EstimateTPSDeformation(Point2f[] controlPoints,
        (Point2f[] templatePts, Point2f[] searchPts) correspondences,
        double lambda, double maxDeformation)
    {
        // 简化版TPS：使用加权平均近似
        var deformed = new Point2f[controlPoints.Length];

        for (int i = 0; i < controlPoints.Length; i++)
        {
            var cp = controlPoints[i];
            var totalWeight = 0.0;
            var dx = 0.0;
            var dy = 0.0;

            for (int j = 0; j < correspondences.templatePts.Length; j++)
            {
                var tp = correspondences.templatePts[j];
                var sp = correspondences.searchPts[j];

                // 基于距离的高斯权重
                var dist = Math.Sqrt(Math.Pow(tp.X - cp.X, 2) + Math.Pow(tp.Y - cp.Y, 2));
                var weight = Math.Exp(-dist * dist / (2 * maxDeformation * maxDeformation));

                dx += (sp.X - tp.X) * weight;
                dy += (sp.Y - tp.Y) * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                dx /= totalWeight;
                dy /= totalWeight;
            }

            // 添加正则化
            dx *= (1 - lambda);
            dy *= (1 - lambda);

            deformed[i] = new Point2f(cp.X + (float)dx, cp.Y + (float)dy);
        }

        return deformed;
    }

    private (Mat warpedImage, Mat mask) ApplyTPSWarp(Mat templateImage, Point2f[] controlPoints,
        Point2f[] deformedPoints, Size outputSize)
    {
        // 使用控制点构建网格并应用形变
        var mapX = new Mat(outputSize, MatType.CV_32FC1);
        var mapY = new Mat(outputSize, MatType.CV_32FC1);
        var mask = new Mat(outputSize, MatType.CV_8UC1, Scalar.All(255));

        // 为每个像素计算对应的源坐标（简化版最近邻）
        for (int y = 0; y < outputSize.Height; y++)
        {
            for (int x = 0; x < outputSize.Width; x++)
            {
                // 找到最近的控制点并插值
                var (srcX, srcY, valid) = TPSInterpolate(x, y, controlPoints, deformedPoints, templateImage.Size());
                mapX.Set(y, x, srcX);
                mapY.Set(y, x, srcY);
                if (!valid)
                {
                    mask.Set(y, x, (byte)0);
                }
            }
        }

        var warped = new Mat();
        Cv2.Remap(templateImage, warped, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

        mapX.Dispose();
        mapY.Dispose();

        return (warped, mask);
    }

    private (float x, float y, bool valid) TPSInterpolate(int dstX, int dstY, Point2f[] controlPoints,
        Point2f[] deformedPoints, Size srcSize)
    {
        // 找到最近的4个控制点并加权
        var dst = new Point2f(dstX, dstY);
        var distances = controlPoints.Select((cp, i) => new
        {
            Index = i,
            Dist = Math.Sqrt(Math.Pow(cp.X - dst.X, 2) + Math.Pow(cp.Y - dst.Y, 2))
        }).OrderBy(d => d.Dist).Take(4).ToList();

        if (distances.Count < 4) return (0, 0, false);

        var totalWeight = distances.Sum(d => 1.0 / (d.Dist + 1));
        var srcX = 0.0f;
        var srcY = 0.0f;

        foreach (var d in distances)
        {
            var weight = (float)(1.0 / (d.Dist + 1) / totalWeight);
            var dp = deformedPoints[d.Index];
            var cp = controlPoints[d.Index];
            // 反推源坐标
            srcX += (dstX - dp.X + cp.X) * weight;
            srcY += (dstY - dp.Y + cp.Y) * weight;
        }

        var valid = srcX >= 0 && srcX < srcSize.Width && srcY >= 0 && srcY < srcSize.Height;
        return (srcX, srcY, valid);
    }

    private (double score, Mat occlusionMask, double meanError) ComputeMatchScoreAndOcclusion(
        Mat warpedImage, Mat warpedMask, Mat searchImage, double occlusionThreshold)
    {
        using var grayWarped = warpedImage.Channels() == 1
            ? warpedImage.Clone()
            : new Mat();
        if (warpedImage.Channels() > 1)
            Cv2.CvtColor(warpedImage, grayWarped, ColorConversionCodes.BGR2GRAY);

        using var graySearch = searchImage.Channels() == 1
            ? searchImage.Clone()
            : new Mat();
        if (searchImage.Channels() > 1)
            Cv2.CvtColor(searchImage, graySearch, ColorConversionCodes.BGR2GRAY);

        // 计算差异
        using var diff = new Mat();
        Cv2.Absdiff(grayWarped, graySearch, diff);

        // 遮挡掩码：差异大于阈值的区域
        var occlusionMask = new Mat();
        Cv2.Threshold(diff, occlusionMask, occlusionThreshold * 255, 255, ThresholdTypes.Binary);

        // 计算分数（归一化互相关）
        using var maskedWarped = new Mat();
        grayWarped.CopyTo(maskedWarped, warpedMask);

        using var result = new Mat();
        Cv2.MatchTemplate(graySearch, maskedWarped, result, TemplateMatchModes.CCoeffNormed);

        var score = result.At<float>(0, 0);
        var meanError = diff.Mean().Val0;

        return (score, occlusionMask, meanError);
    }

    private Point2f[] UpsampleControlPoints(Point2f[] points, double factor)
    {
        return points.Select(p => new Point2f(p.X * (float)factor, p.Y * (float)factor)).ToArray();
    }

    private (double score, Mat mask, double deformation) ValidateFinalMatch(Mat searchImage, Mat templateImage,
        Point2f[] controlPoints, Point2f[] deformedPoints, Mat homography)
    {
        // 应用最终形变
        var (warped, mask) = ApplyTPSWarp(templateImage, controlPoints, deformedPoints, searchImage.Size());

        var (score, occlusionMask, _) = ComputeMatchScoreAndOcclusion(warped, mask, searchImage, 0.3);

        // 计算平均形变量
        var deformation = 0.0;
        for (int i = 0; i < controlPoints.Length; i++)
        {
            deformation += Math.Sqrt(
                Math.Pow(deformedPoints[i].X - controlPoints[i].X, 2) +
                Math.Pow(deformedPoints[i].Y - controlPoints[i].Y, 2));
        }
        deformation /= controlPoints.Length;

        warped.Dispose();
        mask.Dispose();

        return (score, occlusionMask, deformation);
    }

    private double ComputeOcclusionRate(Mat occlusionMask)
    {
        var total = occlusionMask.Total();
        var occluded = Cv2.CountNonZero(occlusionMask);
        return (double)occluded / total;
    }

    private RigidFallbackResult? TryRigidFallback(Mat searchImage, PyramidLevel templateLevel,
        List<DMatch> matches, KeyPoint[] templateKpts, KeyPoint[] searchKpts)
    {
        if (matches.Count < 4) return null;

        var srcPoints = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var dstPoints = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();

        using var homography = Cv2.FindHomography(
            InputArray.Create(srcPoints),
            InputArray.Create(dstPoints),
            HomographyMethods.Ransac,
            3.0);

        if (homography.Empty()) return null;

        // 计算分数
        var corners = new[]
        {
            new Point2f(0, 0),
            new Point2f(templateLevel.Width, 0),
            new Point2f(templateLevel.Width, templateLevel.Height),
            new Point2f(0, templateLevel.Height)
        };

        var transformedCorners = Cv2.PerspectiveTransform(corners, homography);

        return new RigidFallbackResult
        {
            Homography = homography.Clone(),
            Corners = transformedCorners,
            InlierCount = matches.Count,
            Score = 0.5 // 降级匹配的默认分数
        };
    }

    private TemplateData? GetOrLoadTemplate(string path, int pyramidLevels)
    {
        lock (CacheLock)
        {
            if (TemplateCache.TryGetValue(path, out var cached))
            {
                return cached;
            }
        }

        if (!File.Exists(path)) return null;

        using var img = Cv2.ImRead(path, ImreadModes.Color);
        if (img.Empty()) return null;

        var template = BuildTemplatePyramid(img, pyramidLevels);

        lock (CacheLock)
        {
            while (TemplateCache.Count >= 10)
            {
                var oldest = TemplateCache.Keys.First();
                TemplateCache.Remove(oldest);
            }
            TemplateCache[path] = template;
        }

        return template;
    }

    private OperatorExecutionOutput CreateSuccessOutput(Mat image, IReadOnlyList<DeformableMatchResult> matches,
        TemplateData template, long processingTime)
    {
        var bestMatch = matches[0];
        var resultImage = image.Clone();

        // 绘制形变网格
        if (bestMatch.ControlPoints != null && bestMatch.DeformedPoints != null)
        {
            DrawDeformationGrid(resultImage, bestMatch.ControlPoints, bestMatch.DeformedPoints, new Scalar(0, 255, 0));
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var corners = match.Corners;
            if (corners == null || corners.Length != 4)
            {
                continue;
            }

            var color = index == 0 ? new Scalar(0, 255, 0) : new Scalar(0, 165, 255);
            for (int i = 0; i < 4; i++)
            {
                var pt1 = new Point((int)corners[i].X, (int)corners[i].Y);
                var pt2 = new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y);
                Cv2.Line(resultImage, pt1, pt2, color, index == 0 ? 2 : 1);
            }
        }

        // 绘制信息
        Cv2.PutText(resultImage, $"Matches: {matches.Count}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        Cv2.PutText(resultImage, $"Best Score: {bestMatch.Score:F3}", new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        Cv2.PutText(resultImage, $"Best Occlusion: {bestMatch.OcclusionRate:P1}", new Point(10, 90),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        Cv2.PutText(resultImage, $"Best Deformation: {bestMatch.DeformationMagnitude:F1}px", new Point(10, 120),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var serializedMatches = matches.Select(BuildMatchPayload).ToList();
        var bestPayload = serializedMatches[0];

        var resultData = new Dictionary<string, object>
        {
            { "IsMatch", true },
            { "Score", bestMatch.Score },
            { "OcclusionRate", bestMatch.OcclusionRate },
            { "DeformationMagnitude", bestMatch.DeformationMagnitude },
            { "ControlPoints", bestMatch.ControlPoints?.Select(p => new Position(p.X, p.Y)).ToList() },
            { "ProcessingTimeMs", processingTime },
            { "Method", bestMatch.IsFallback ? "Rigid_Fallback" : "TPS_Deformable" },
            { "MatchCount", matches.Count },
            { "Matches", serializedMatches },
            { "MatchResult", bestPayload },
            { "DeformationField", CreateDeformationFieldPayload(bestMatch) },
            { "Message", matches.Count > 1 ? "Multi-target deformable matching successful" : "Deformable match successful" }
        };

        if (bestMatch.OcclusionMask != null && !bestMatch.OcclusionMask.Empty())
        {
            resultData["OcclusionMask"] = bestMatch.OcclusionMask.Clone();
        }

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, resultData));
    }

    private OperatorExecutionOutput CreateFallbackOutput(Mat image, DeformableMatchResult result,
        TemplateData template, long processingTime)
    {
        var resultImage = image.Clone();
        var fallback = result.RigidFallbackResult;

        if (fallback != null)
        {
            // 绘制刚性变换结果
            for (int i = 0; i < 4; i++)
            {
                var pt1 = new Point((int)fallback.Corners[i].X, (int)fallback.Corners[i].Y);
                var pt2 = new Point((int)fallback.Corners[(i + 1) % 4].X, (int)fallback.Corners[(i + 1) % 4].Y);
                Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 165, 255), 2);
            }
        }

        Cv2.PutText(resultImage, "FALLBACK: Rigid Transform", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 165, 255), 2);
        Cv2.PutText(resultImage, $"Score: {fallback?.Score:F3}", new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 165, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "IsMatch", true },
            { "Score", result.Score },
            { "Method", "Rigid_Fallback" },
            { "MatchCount", 1 },
            { "Matches", new List<Dictionary<string, object>> { BuildMatchPayload(result) } },
            { "MatchResult", BuildMatchPayload(result) },
            { "ProcessingTimeMs", processingTime },
            { "OriginalFailureReason", result.FailureReason },
            { "Message", "Deformable matching failed, fell back to rigid transform" }
        }));
    }

    private OperatorExecutionOutput CreateDetailedFailureOutput(Mat image, DeformableMatchResult result, long processingTime)
    {
        var resultImage = image.Clone();
        Cv2.PutText(resultImage, $"NG: {result.FailureReason}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "FailureReason", result.FailureReason },
            { "MatchCount", 0 },
            { "Matches", Array.Empty<object>() },
            { "ProcessingTimeMs", processingTime },
            { "Message", result.FailureReason }
        }));
    }

    private OperatorExecutionOutput CreateFailureOutput(Mat image, string reason, long processingTime)
    {
        var resultImage = image.Clone();
        Cv2.PutText(resultImage, $"NG: {reason}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "FailureReason", reason },
            { "MatchCount", 0 },
            { "Matches", Array.Empty<object>() },
            { "ProcessingTimeMs", processingTime },
            { "Message", reason }
        }));
    }

    private static Dictionary<string, object> BuildMatchPayload(DeformableMatchResult match)
    {
        var boundingBox = match.BoundingBox;
        return new Dictionary<string, object>
        {
            { "Score", match.Score },
            { "OcclusionRate", match.OcclusionRate },
            { "DeformationMagnitude", match.DeformationMagnitude },
            { "CandidateScore", match.CandidateScore },
            { "IsFallback", match.IsFallback },
            { "BoundingBox", new { boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height } },
            { "Corners", match.Corners?.Select(point => new Position(point.X, point.Y)).ToList() ?? new List<Position>() }
        };
    }

    private static object CreateDeformationFieldPayload(DeformableMatchResult match)
    {
        return new
        {
            ControlPoints = match.ControlPoints?.Select(point => new Position(point.X, point.Y)).ToList() ?? new List<Position>(),
            DeformedPoints = match.DeformedPoints?.Select(point => new Position(point.X, point.Y)).ToList() ?? new List<Position>()
        };
    }

    private void DrawDeformationGrid(Mat image, Point2f[] controlPoints, Point2f[] deformedPoints, Scalar color)
    {
        // 绘制控制点连线
        var gridSize = (int)Math.Sqrt(controlPoints.Length);
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                var idx = y * gridSize + x;
                if (x < gridSize - 1)
                {
                    var pt1 = new Point((int)deformedPoints[idx].X, (int)deformedPoints[idx].Y);
                    var pt2 = new Point((int)deformedPoints[idx + 1].X, (int)deformedPoints[idx + 1].Y);
                    Cv2.Line(image, pt1, pt2, color, 1);
                }
                if (y < gridSize - 1)
                {
                    var pt1 = new Point((int)deformedPoints[idx].X, (int)deformedPoints[idx].Y);
                    var pt2 = new Point((int)deformedPoints[idx + gridSize].X, (int)deformedPoints[idx + gridSize].Y);
                    Cv2.Line(image, pt1, pt2, color, 1);
                }
            }
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetDoubleParam(@operator, "MinMatchScore", 0.6);
        if (minScore < 0 || minScore > 1)
        {
            return ValidationResult.Invalid("MinMatchScore must be between 0 and 1.");
        }

        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3);
        if (pyramidLevels < 1 || pyramidLevels > 6)
        {
            return ValidationResult.Invalid("PyramidLevels must be between 1 and 6.");
        }

        var maxMatches = GetIntParam(@operator, "MaxMatches", 5);
        if (maxMatches < 1 || maxMatches > 20)
        {
            return ValidationResult.Invalid("MaxMatches must be between 1 and 20.");
        }

        var candidateThreshold = GetDoubleParam(@operator, "CandidateThreshold", 0.65);
        if (candidateThreshold <= 0 || candidateThreshold > 1)
        {
            return ValidationResult.Invalid("CandidateThreshold must be between 0 and 1.");
        }

        var nmsThreshold = GetDoubleParam(@operator, "NmsThreshold", 0.35);
        if (nmsThreshold < 0 || nmsThreshold > 1)
        {
            return ValidationResult.Invalid("NmsThreshold must be between 0 and 1.");
        }

        return ValidationResult.Valid();
    }

    private class TemplateData
    {
        public Mat BaseImage { get; set; } = new Mat();
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PyramidLevel> Pyramid { get; set; } = new();
    }

    private class PyramidLevel
    {
        public Mat Image { get; set; } = new Mat();
        public Mat Descriptors { get; set; } = new Mat();
        public KeyPoint[] KeyPoints { get; set; } = Array.Empty<KeyPoint>();
        public int Width { get; set; }
        public int Height { get; set; }
        public double Scale { get; set; }
    }

    private class DeformableMatchResult
    {
        public bool IsSuccess { get; set; }
        public bool IsFallback { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public double Score { get; set; }
        public double CandidateScore { get; set; }
        public double OcclusionRate { get; set; }
        public double DeformationMagnitude { get; set; }
        public Rect BoundingBox { get; set; }
        public Point2f[]? Corners { get; set; }
        public Point2f[]? ControlPoints { get; set; }
        public Point2f[]? DeformedPoints { get; set; }
        public Mat? Homography { get; set; }
        public Mat? OcclusionMask { get; set; }
        public RigidFallbackResult? RigidFallbackResult { get; set; }
    }

    private class RigidFallbackResult
    {
        public Mat Homography { get; set; } = new Mat();
        public Point2f[] Corners { get; set; } = Array.Empty<Point2f>();
        public int InlierCount { get; set; }
        public double Score { get; set; }
    }

    private readonly record struct CandidateWindow(Rect Roi, Rect Proposal, double Score);
}
