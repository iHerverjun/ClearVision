// LocalDeformableMatchingOperator.cs
// 灞€閮ㄥ彲鍙樺舰鍖归厤绠楀瓙 (瀹為獙绾?MVP)
// 瀵规爣 Halcon: find_local_deformable_model
// 浣滆€咃細AI Assistant

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
/// 灞€閮ㄥ彲鍙樺舰鍖归厤绠楀瓙 - 鏀寔灞€閮ㄥ舰鍙樺拰閬尅鐨勬ā鏉垮尮閰?
/// 瀵规爣 Halcon find_local_deformable_model
/// Phase 4.2: 鎵╁睍涓哄鐩爣鍊欓€夈€丯MS 鍘婚噸鍜屾壒閲忓彉褰㈣瘎浼?
/// </summary>
[OperatorMeta(
    DisplayName = "Local Deformable Matching",
    Description = "Experimental local deformable matching backed by moving least squares deformation and verified rigid fallback.",
    Category = "Matching",
    IconName = "deformable-match",
    Keywords = new[] { "Deformable", "Local", "Matching", "MLS", "Occlusion", "MultiTarget", "NMS" },
    Version = "1.1.0"
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
[OperatorParam("TPSGridSize", "Control Grid Size", "int", DefaultValue = 4, Min = 2, Max = 8)]
[OperatorParam("TPSLambda", "MLS Smoothing (Legacy TPSLambda)", "double", DefaultValue = 0.01, Min = 0.001, Max = 1.0)]
[OperatorParam("MaxDeformation", "Max Deformation (px)", "double", DefaultValue = 20.0, Min = 5.0, Max = 100.0)]
[OperatorParam("OcclusionThreshold", "Occlusion Threshold", "double", DefaultValue = 0.3, Min = 0.1, Max = 0.9)]
[OperatorParam("MinMatchScore", "Min Match Score", "double", DefaultValue = 0.6, Min = 0.0, Max = 1.0)]
[OperatorParam("EnableFallback", "Enable Fallback to Rigid", "bool", DefaultValue = false)]
[OperatorParam("MaxIterations", "Max Refinement Iterations", "int", DefaultValue = 5, Min = 1, Max = 20)]
[OperatorParam("ConvergenceThreshold", "Convergence Threshold", "double", DefaultValue = 0.5, Min = 0.1, Max = 5.0)]
[OperatorParam("MaxMatches", "Maximum Matches", "int", DefaultValue = 5, Min = 1, Max = 20)]
[OperatorParam("CandidateThreshold", "Candidate Seed Threshold", "double", DefaultValue = 0.65, Min = 0.1, Max = 1.0)]
[OperatorParam("EnableNms", "Enable NMS", "bool", DefaultValue = true)]
[OperatorParam("NmsThreshold", "NMS IoU Threshold", "double", DefaultValue = 0.35, Min = 0.0, Max = 1.0)]
[OperatorParam("ParallelCandidates", "Parallel Candidate Evaluation", "bool", DefaultValue = true)]
public class LocalDeformableMatchingOperator : OperatorBase
{
    private const int TemplateCacheCapacity = 10;
    private const double OcclusionDifferenceThreshold = 32.0;
    public override OperatorType OperatorType => OperatorType.LocalDeformableMatching;

    // 妯℃澘缂撳瓨
    private static readonly Dictionary<string, TemplateData> TemplateCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> TemplateCacheOrder = new();
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

        // 鍙傛暟
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, 1, 6);
        var tpsGridSize = GetIntParam(@operator, "TPSGridSize", 4, 2, 8);
        var tpsLambda = GetDoubleParam(@operator, "TPSLambda", 0.01, 0.001, 1.0);
        var maxDeformation = GetDoubleParam(@operator, "MaxDeformation", 20.0, 5.0, 100.0);
        var occlusionThreshold = GetDoubleParam(@operator, "OcclusionThreshold", 0.3, 0.1, 0.9);
        var minMatchScore = GetDoubleParam(@operator, "MinMatchScore", 0.6, 0.0, 1.0);
        var enableFallback = GetBoolParam(@operator, "EnableFallback", false);
        var maxIterations = GetIntParam(@operator, "MaxIterations", 5, 1, 20);
        var convergenceThreshold = GetDoubleParam(@operator, "ConvergenceThreshold", 0.5, 0.1, 5.0);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 5, 1, 20);
        var candidateThreshold = GetDoubleParam(@operator, "CandidateThreshold", 0.65, 0.1, 1.0);
        var enableNms = GetBoolParam(@operator, "EnableNms", true);
        var nmsThreshold = GetDoubleParam(@operator, "NmsThreshold", 0.35, 0.0, 1.0);
        var parallelCandidates = GetBoolParam(@operator, "ParallelCandidates", true);

        // 鑾峰彇鍥惧儚
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var searchImage = imageWrapper.GetMat();
        if (searchImage.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        // 鑾峰彇妯℃澘
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

        List<DeformableMatchResult>? candidateResults = null;
        try
        {
            var candidates = GenerateCandidateWindows(searchImage, template.BaseImage, maxMatches, candidateThreshold, maxDeformation);
            candidateResults = EvaluateCandidates(
                searchImage, template, candidates, parallelCandidates,
                pyramidLevels, tpsGridSize, tpsLambda, maxDeformation,
                occlusionThreshold, minMatchScore, enableFallback,
                maxIterations, convergenceThreshold, cancellationToken);

            var acceptedMatches = candidateResults
                .Where(result => result.IsSuccess && result.VerificationPassed && result.Score >= minMatchScore)
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
        finally
        {
            if (candidateResults != null)
            {
                foreach (var result in candidateResults)
                {
                    result.Dispose();
                }
            }

            template.Dispose();
        }
    }

    private DeformableMatchResult PerformCoarseToFineMatching(
        Mat searchImage, TemplateData template, int pyramidLevels, int tpsGridSize,
        double tpsLambda, double maxDeformation, double occlusionThreshold,
        double minMatchScore, int maxIterations, double convergenceThreshold,
        CancellationToken cancellationToken)
    {
        var result = new DeformableMatchResult
        {
            ControlGridSize = tpsGridSize,
            MlsLambda = tpsLambda,
            OcclusionThreshold = occlusionThreshold
        };
        var currentLevel = pyramidLevels - 1;

        // 鏋勫缓鎼滅储鍥惧儚閲戝瓧濉?
        var searchPyramid = BuildImagePyramid(searchImage, pyramidLevels);

        // 浠庣矖鍒扮粏杩唬
        Mat? currentHomography = null;
        Point2f[]? controlPoints = null;
        Point2f[]? deformedPoints = null;
        var bestVerifiedMatchCount = 0;
        var bestVerifiedInlierRatio = 0.0;
        try
        {
            for (var level = currentLevel; level >= 0; level--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var levelTemplate = template.Pyramid[level];
                var levelSearch = searchPyramid[level];

                // 缂╂斁鎺у埗鐐瑰埌褰撳墠灞傜骇
                var scale = Math.Pow(2, level);
                if (controlPoints == null)
                {
                    // 鍒濆鍖栧潎鍖€缃戞牸鎺у埗鐐?
                    controlPoints = InitializeControlPoints(
                        levelTemplate.Width, levelTemplate.Height, tpsGridSize);
                }

                // 鐗瑰緛鍖归厤
                var (matches, templateKpts, searchKpts) = MatchFeaturesAtLevel(
                    levelTemplate.Image, levelSearch, levelTemplate.KeyPoints, levelTemplate.Descriptors);

                if (matches.Count < 10)
                {
                    if (level > 0 && currentHomography == null)
                    {
                        continue;
                    }

                    result.FailureReason = $"Insufficient matches at level {level}: {matches.Count}";
                    result.RigidFallbackResult = TryRigidFallback(
                        levelSearch, levelTemplate, matches, templateKpts, searchKpts);
                    return result;
                }

                // 浼拌鍒濆鍒氭€у彉鎹紙椴佹浼拌锛?
                if (currentHomography == null)
                {
                    currentHomography = EstimateRigidTransform(
                        matches, templateKpts, searchKpts, new Size(levelTemplate.Width, levelTemplate.Height), levelSearch.Size(), maxDeformation / scale);
                    if (currentHomography == null)
                    {
                        if (level > 0)
                        {
                            continue;
                        }

                        result.FailureReason = "Failed to estimate initial rigid transform";
                        return result;
                    }
                }

                // TPS褰㈠彉缁嗗寲
                var levelBaselineDeformedPoints = deformedPoints?.ToArray()
                    ?? Cv2.PerspectiveTransform(controlPoints, currentHomography);
                var iterationDeformedPoints = levelBaselineDeformedPoints.ToArray();
                var iteration = 0;
                var converged = false;
                var prevError = double.MaxValue;
                var bestIterationScore = double.NegativeInfinity;
                var bestIterationError = double.PositiveInfinity;
                Point2f[]? bestIterationDeformedPoints = iterationDeformedPoints.ToArray();

                while (iteration < maxIterations && !converged)
                {
                    // 璁＄畻褰撳墠鍙樻崲涓嬬殑鐗瑰緛鐐瑰搴?
                    var correspondences = ComputeCorrespondences(
                        matches, templateKpts, searchKpts, controlPoints, iterationDeformedPoints, currentHomography);

                    // 浼拌褰㈠彉鍦?
                    var refinedDeformedPoints = EstimateTPSDeformation(
                        iterationDeformedPoints, correspondences, tpsLambda, maxDeformation / scale);

                    // 搴旂敤褰㈠彉骞惰绠楄宸?
                    var (warpedImage, warpedMask) = ApplyTPSWarp(
                        levelTemplate.Image, controlPoints, refinedDeformedPoints, levelSearch.Size());

                    // 璁＄畻鍖归厤鍒嗘暟鍜岄伄鎸?
                    var (score, occlusionMask, meanError, _) = ComputeMatchScoreAndOcclusion(
                        warpedImage, warpedMask, levelSearch, occlusionThreshold);

                    if (score > bestIterationScore || (Math.Abs(score - bestIterationScore) < 1e-6 && meanError < bestIterationError))
                    {
                        bestIterationScore = score;
                        bestIterationError = meanError;
                        bestIterationDeformedPoints = refinedDeformedPoints.ToArray();
                    }

                    if (iteration > 0 && Math.Abs(prevError - meanError) < convergenceThreshold)
                    {
                        converged = true;
                    }

                    iterationDeformedPoints = refinedDeformedPoints;
                    prevError = meanError;
                    iteration++;

                    warpedImage.Dispose();
                    warpedMask.Dispose();
                    occlusionMask.Dispose();
                }

                if (bestIterationDeformedPoints != null)
                {
                    deformedPoints = bestIterationDeformedPoints;
                }

                bestVerifiedMatchCount = Math.Max(bestVerifiedMatchCount, matches.Count);
                bestVerifiedInlierRatio = Math.Max(bestVerifiedInlierRatio, matches.Count / (double)Math.Max(1, levelTemplate.KeyPoints.Length));

                // 涓婇噰鏍峰埌涓嬩竴灞?
                if (level > 0)
                {
                    controlPoints = UpsampleControlPoints(controlPoints, 2.0);
                    if (deformedPoints != null)
                    {
                        deformedPoints = UpsampleControlPoints(deformedPoints, 2.0);
                    }
                }
            }

            // 鏈€缁堥獙璇?
            if (currentHomography != null && controlPoints != null && deformedPoints != null)
            {
                var (finalScore, finalOcclusionMask, finalOcclusionRate, finalDeformation) = ValidateFinalMatch(
                    searchImage,
                    template.BaseImage,
                    controlPoints,
                    deformedPoints,
                    currentHomography,
                    occlusionThreshold);

                result.OcclusionThreshold = occlusionThreshold;
                result.OcclusionRate = finalOcclusionRate;
                result.IsSuccess = finalScore >= minMatchScore && finalOcclusionRate <= occlusionThreshold;
                result.VerificationPassed = result.IsSuccess;
                result.Score = finalScore;
                result.VerificationScore = result.IsSuccess ? finalScore : 0.0;
                result.InlierCount = bestVerifiedMatchCount;
                result.InlierRatio = Math.Clamp(bestVerifiedInlierRatio, 0.0, 1.0);
                result.DeformationMagnitude = finalDeformation;
                result.ControlPoints = controlPoints;
                result.DeformedPoints = deformedPoints;
                result.Homography = currentHomography;
                currentHomography = null;
                result.OcclusionMask = finalOcclusionMask;

                if (!result.IsSuccess)
                {
                    result.FailureReason = finalOcclusionRate > occlusionThreshold
                        ? $"Occlusion rate {finalOcclusionRate:F3} exceeded threshold {occlusionThreshold:F3}"
                        : $"Final score {finalScore:F3} below threshold {minMatchScore}";
                }
            }
            else
            {
                result.FailureReason = "Failed to compute deformation field";
            }

            return result;
        }
        finally
        {
            currentHomography?.Dispose();
            foreach (var level in searchPyramid)
            {
                level.Dispose();
            }
        }
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
        if (fallback == null || !fallback.VerificationPassed)
        {
            return;
        }

        result.IsSuccess = true;
        result.IsFallback = true;
        result.VerificationPassed = true;
        result.Score = fallback.Score;
        result.VerificationScore = fallback.VerificationScore;
        result.InlierCount = fallback.InlierCount;
        result.InlierRatio = fallback.InlierRatio;
        result.OcclusionRate = 0.0;
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
        return union <= 0 ? 0 : intersection / (double)union;
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
        try
        {
            for (var i = 0; i < levels; i++)
            {
                using var gray = currentImage.Channels() == 1
                    ? currentImage.Clone()
                    : new Mat();
                if (currentImage.Channels() > 1)
                {
                    Cv2.CvtColor(currentImage, gray, ColorConversionCodes.BGR2GRAY);
                }

                // 鎻愬彇鐗瑰緛
                using var orb = ORB.Create(1000, 1.2f, 8);
                KeyPoint[] keypoints;
                var descriptors = new Mat();
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

                // 涓嬮噰鏍?
                if (i < levels - 1)
                {
                    using var nextImage = new Mat();
                    Cv2.PyrDown(currentImage, nextImage);
                    currentImage.Dispose();
                    currentImage = nextImage.Clone();
                }
            }
        }
        catch
        {
            data.Dispose();
            throw;
        }
        finally
        {
            currentImage.Dispose();
        }

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
                // 杈圭晫澶勭悊
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
        // 鎻愬彇鎼滅储鍥惧儚鐗瑰緛
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

        // KNN鍖归厤
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
        KeyPoint[] searchKpts, Size templateSize, Size searchImageSize, double maxDeformation)
    {
        if (matches.Count < 4)
        {
            return null;
        }

        var srcPoints = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var dstPoints = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();
        var verified = HomographyVerificationHelper.TryEstimateAndVerify(
            srcPoints,
            dstPoints,
            templateSize,
            searchImageSize,
            Math.Max(2.0, Math.Min(6.0, maxDeformation)),
            minMatchCount: 4,
            minInliers: 4,
            minInlierRatio: 0.10,
            out var homography,
            out _,
            out _);

        return verified && homography != null && !homography.Empty() ? homography : null;
    }

    private (Point2f[] templatePts, Point2f[] searchPts) ComputeCorrespondences(
        List<DMatch> matches, KeyPoint[] templateKpts, KeyPoint[] searchKpts,
        Point2f[] controlPoints, Point2f[] currentDeformedPoints, Mat homography)
    {
        var templatePts = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var searchPts = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();

        if (currentDeformedPoints.Length == controlPoints.Length &&
            LocalDeformableMovingLeastSquaresHelper.TryCreate(
                controlPoints,
                currentDeformedPoints,
                1e-3,
                out var currentModel,
                out _))
        {
            var projectedTemplatePts = currentModel!.MapForward(templatePts);
            return (projectedTemplatePts, searchPts);
        }

        return AddDeformationAnchors(templatePts, searchPts, controlPoints, homography);
    }

    private Point2f[] EstimateTPSDeformation(Point2f[] controlPoints,
        (Point2f[] templatePts, Point2f[] searchPts) correspondences,
        double lambda, double maxDeformation, Point2f[]? clampReferencePoints = null)
    {
        if (!LocalDeformableMovingLeastSquaresHelper.TryCreate(
                correspondences.templatePts,
                correspondences.searchPts,
                Math.Max(1e-4, lambda),
                out var model,
                out _))
        {
            return controlPoints.ToArray();
        }

        var referencePoints = clampReferencePoints != null && clampReferencePoints.Length == controlPoints.Length
            ? clampReferencePoints
            : controlPoints;

        return model!.MapForward(controlPoints)
            .Select((point, index) =>
            {
                var dx = point.X - referencePoints[index].X;
                var dy = point.Y - referencePoints[index].Y;
                var magnitude = Math.Sqrt((dx * dx) + (dy * dy));
                if (magnitude <= maxDeformation || magnitude <= 1e-6)
                {
                    return point;
                }

                var scale = maxDeformation / magnitude;
                return new Point2f(
                    referencePoints[index].X + (float)(dx * scale),
                    referencePoints[index].Y + (float)(dy * scale));
            })
            .ToArray();
    }

    private (Mat warpedImage, Mat mask) ApplyTPSWarp(Mat templateImage, Point2f[] controlPoints,
        Point2f[] deformedPoints, Size outputSize)
    {
        if (!LocalDeformableMovingLeastSquaresHelper.TryCreate(
                controlPoints,
                deformedPoints,
                1e-3,
                out var model,
                out _))
        {
            return (new Mat(), new Mat(outputSize, MatType.CV_8UC1, Scalar.All(0)));
        }

        return model!.Warp(templateImage, outputSize);
    }

    private (double score, Mat occlusionMask, double meanError, double occlusionRate) ComputeMatchScoreAndOcclusion(
        Mat warpedImage, Mat warpedMask, Mat searchImage, double occlusionThreshold)
    {
        using var grayWarped = warpedImage.Channels() == 1 ? warpedImage.Clone() : warpedImage.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var graySearch = searchImage.Channels() == 1 ? searchImage.Clone() : searchImage.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var diff = new Mat();
        Cv2.Absdiff(grayWarped, graySearch, diff);

        using var rawOcclusion = new Mat();
        Cv2.Threshold(diff, rawOcclusion, OcclusionDifferenceThreshold, 255, ThresholdTypes.Binary);
        var occlusionMask = new Mat(warpedMask.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.BitwiseAnd(rawOcclusion, warpedMask, occlusionMask);

        using var invertedOcclusion = new Mat();
        Cv2.BitwiseNot(occlusionMask, invertedOcclusion);
        using var visibleMask = new Mat();
        Cv2.BitwiseAnd(invertedOcclusion, warpedMask, visibleMask);

        var visiblePixels = Cv2.CountNonZero(visibleMask);
        if (visiblePixels == 0)
        {
            return (0, occlusionMask, double.PositiveInfinity, 1.0);
        }

        var occlusionRate = ComputeOcclusionRate(occlusionMask, warpedMask);
        var normalizedCorrelation = (ComputeMaskedCorrelation(grayWarped, graySearch, visibleMask) + 1.0) * 0.5;
        var meanError = diff.Mean(visibleMask).Val0;
        var visibleRatio = visiblePixels / (double)Math.Max(1, Cv2.CountNonZero(warpedMask));
        var errorScore = 1.0 - Math.Clamp(meanError / 255.0, 0.0, 1.0);
        var baseScore = Math.Clamp((normalizedCorrelation * 0.7) + (visibleRatio * 0.2) + (errorScore * 0.1), 0.0, 1.0);
        var occlusionPenalty = occlusionRate <= occlusionThreshold
            ? 1.0
            : Math.Max(0.0, 1.0 - ((occlusionRate - occlusionThreshold) / Math.Max(1e-6, 1.0 - occlusionThreshold)));
        var score = Math.Clamp(baseScore * occlusionPenalty, 0.0, 1.0);
        return (score, occlusionMask, meanError, occlusionRate);
    }

    private Point2f[] UpsampleControlPoints(Point2f[] points, double factor)
    {
        return points.Select(p => new Point2f(p.X * (float)factor, p.Y * (float)factor)).ToArray();
    }

    private (double score, Mat mask, double occlusionRate, double deformation) ValidateFinalMatch(Mat searchImage, Mat templateImage,
        Point2f[] controlPoints, Point2f[] deformedPoints, Mat homography, double occlusionThreshold)
    {
        var (warped, mask) = ApplyTPSWarp(templateImage, controlPoints, deformedPoints, searchImage.Size());
        var (score, occlusionMask, _, occlusionRate) = ComputeMatchScoreAndOcclusion(warped, mask, searchImage, occlusionThreshold);

        var deformation = 0.0;
        for (var index = 0; index < controlPoints.Length; index++)
        {
            var dx = deformedPoints[index].X - controlPoints[index].X;
            var dy = deformedPoints[index].Y - controlPoints[index].Y;
            deformation += Math.Sqrt((dx * dx) + (dy * dy));
        }

        deformation /= Math.Max(1, controlPoints.Length);
        warped.Dispose();
        mask.Dispose();
        return (score, occlusionMask, occlusionRate, deformation);
    }

    private double ComputeOcclusionRate(Mat occlusionMask, Mat supportMask)
    {
        var total = Math.Max(1.0, Cv2.CountNonZero(supportMask));
        return Cv2.CountNonZero(occlusionMask) / total;
    }

    private static (Point2f[] templatePts, Point2f[] searchPts) AddDeformationAnchors(
        Point2f[] templatePts,
        Point2f[] searchPts,
        Point2f[] controlPoints,
        Mat homography)
    {
        var mergedTemplate = templatePts.ToList();
        var mergedSearch = searchPts.ToList();
        var projectedControls = Cv2.PerspectiveTransform(controlPoints, homography);
        for (var index = 0; index < controlPoints.Length; index++)
        {
            var controlPoint = controlPoints[index];
            if (mergedTemplate.Any(existing => Math.Abs(existing.X - controlPoint.X) < 1e-3 && Math.Abs(existing.Y - controlPoint.Y) < 1e-3))
            {
                continue;
            }

            mergedTemplate.Add(controlPoint);
            mergedSearch.Add(projectedControls[index]);
        }

        return (mergedTemplate.ToArray(), mergedSearch.ToArray());
    }
    private static double ComputeMaskedCorrelation(Mat warpedGray, Mat searchGray, Mat visibleMask)
    {
        var sumWarped = 0.0;
        var sumSearch = 0.0;
        var count = 0;
        for (var y = 0; y < visibleMask.Rows; y++)
        {
            for (var x = 0; x < visibleMask.Cols; x++)
            {
                if (visibleMask.Get<byte>(y, x) == 0)
                {
                    continue;
                }

                sumWarped += warpedGray.Get<byte>(y, x);
                sumSearch += searchGray.Get<byte>(y, x);
                count++;
            }
        }

        if (count == 0)
        {
            return 0;
        }

        var meanWarped = sumWarped / count;
        var meanSearch = sumSearch / count;
        var numerator = 0.0;
        var denomWarped = 0.0;
        var denomSearch = 0.0;
        for (var y = 0; y < visibleMask.Rows; y++)
        {
            for (var x = 0; x < visibleMask.Cols; x++)
            {
                if (visibleMask.Get<byte>(y, x) == 0)
                {
                    continue;
                }

                var centeredWarped = warpedGray.Get<byte>(y, x) - meanWarped;
                var centeredSearch = searchGray.Get<byte>(y, x) - meanSearch;
                numerator += centeredWarped * centeredSearch;
                denomWarped += centeredWarped * centeredWarped;
                denomSearch += centeredSearch * centeredSearch;
            }
        }

        return denomWarped <= 1e-6 || denomSearch <= 1e-6 ? 0 : Math.Clamp(numerator / Math.Sqrt(denomWarped * denomSearch), -1.0, 1.0);
    }

    private RigidFallbackResult? TryRigidFallback(Mat searchImage, PyramidLevel templateLevel,
        List<DMatch> matches, KeyPoint[] templateKpts, KeyPoint[] searchKpts)
    {
        if (matches.Count < 4) return null;

        var srcPoints = matches.Select(m => templateKpts[m.QueryIdx].Pt).ToArray();
        var dstPoints = matches.Select(m => searchKpts[m.TrainIdx].Pt).ToArray();

        var verified = HomographyVerificationHelper.TryEstimateAndVerify(
            srcPoints,
            dstPoints,
            new Size(templateLevel.Width, templateLevel.Height),
            searchImage.Size(),
            3.0,
            minMatchCount: 4,
            minInliers: 4,
            minInlierRatio: 0.10,
            out var homography,
            out var transformedCorners,
            out var metrics);

        // 璁＄畻鍒嗘暟
        #if false
        var corners = new[]
        {
            new Point2f(0, 0),
            new Point2f(templateLevel.Width, 0),
            new Point2f(templateLevel.Width, templateLevel.Height),
            new Point2f(0, templateLevel.Height)
        };

        var transformedCorners = Cv2.PerspectiveTransform(corners, homography);
        #endif

        return new RigidFallbackResult
        {
            VerificationPassed = verified,
            FailureReason = metrics.FailureReason,
            InlierRatio = metrics.InlierRatio,
            VerificationScore = HomographyVerificationHelper.ComputeVerificationScore(metrics, 3.0),
            Homography = homography ?? new Mat(),
            Corners = transformedCorners,
            InlierCount = metrics.InlierCount,
            Score = HomographyVerificationHelper.ComputeVerificationScore(metrics, 3.0)
        };
    }

    private TemplateData? GetOrLoadTemplate(string path, int pyramidLevels)
    {
        var cacheKey = BuildTemplateCacheKey(path, pyramidLevels);
        lock (CacheLock)
        {
            if (TemplateCache.TryGetValue(cacheKey, out var cached))
            {
                TouchTemplateCacheEntry(cacheKey, cached);
                return cached.Clone();
            }
        }

        if (!File.Exists(path)) return null;

        using var img = Cv2.ImRead(path, ImreadModes.Color);
        if (img.Empty()) return null;

        var template = BuildTemplatePyramid(img, pyramidLevels);

        lock (CacheLock)
        {
            if (TemplateCache.TryGetValue(cacheKey, out var existing))
            {
                template.Dispose();
                TouchTemplateCacheEntry(cacheKey, existing);
                return existing.Clone();
            }

            while (TemplateCache.Count >= TemplateCacheCapacity)
            {
                var oldest = TemplateCacheOrder.First?.Value;
                if (oldest == null)
                {
                    break;
                }

                if (TemplateCache.Remove(oldest, out var evicted))
                {
                    TemplateCacheOrder.RemoveFirst();
                    evicted.Dispose();
                }
            }

            template.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
            TemplateCache[cacheKey] = template;
        }

        return template.Clone();
    }

    private OperatorExecutionOutput CreateSuccessOutput(Mat image, IReadOnlyList<DeformableMatchResult> matches,
        TemplateData template, long processingTime)
    {
        var bestMatch = matches[0];
        var resultImage = image.Clone();

        // 缁樺埗褰㈠彉缃戞牸
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

        // 缁樺埗淇℃伅
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
            { "FailureReason", string.Empty },
            { "VerificationPassed", bestMatch.VerificationPassed || bestMatch.IsFallback },
            { "OcclusionRate", bestMatch.OcclusionRate },
            { "OcclusionThreshold", bestMatch.OcclusionThreshold },
            { "DeformationMagnitude", bestMatch.DeformationMagnitude },
            { "InlierCount", bestMatch.InlierCount },
            { "InlierRatio", bestMatch.InlierRatio },
            { "ControlPoints", bestMatch.ControlPoints?.Select(p => new Position(p.X, p.Y)).ToList() ?? new List<Position>() },
            { "ProcessingTimeMs", processingTime },
            { "Method", bestMatch.IsFallback ? "Rigid_Fallback" : "MLS_Deformable" },
            { "DeformationModel", bestMatch.IsFallback ? "RigidHomography" : "MovingLeastSquaresSimilarity" },
            { "LegacyParameterCompatibility", CreateLegacyParameterCompatibilityPayload(bestMatch) },
            { "MatchCount", matches.Count },
            { "Matches", serializedMatches },
            { "MatchResult", bestPayload },
            { "DeformationField", CreateDeformationFieldPayload(bestMatch) },
            { "Message", matches.Count > 1 ? "Multi-target MLS deformable matching successful" : "MLS deformable match successful" }
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
            // 缁樺埗鍒氭€у彉鎹㈢粨鏋?
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
            { "DeformationModel", "RigidHomography" },
            { "FailureReason", string.Empty },
            { "VerificationPassed", true },
            { "MatchCount", 1 },
            { "InlierCount", fallback?.InlierCount ?? 0 },
            { "InlierRatio", fallback?.InlierRatio ?? 0.0 },
            { "OcclusionRate", result.OcclusionRate },
            { "OcclusionThreshold", result.OcclusionThreshold },
            { "LegacyParameterCompatibility", CreateLegacyParameterCompatibilityPayload(result) },
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
            { "Method", result.IsFallback ? "Rigid_Fallback" : "MLS_Deformable" },
            { "FailureReason", result.FailureReason },
            { "VerificationPassed", false },
            { "MatchCount", 0 },
            { "InlierCount", result.InlierCount },
            { "InlierRatio", result.InlierRatio },
            { "Score", result.Score },
            { "OcclusionRate", result.OcclusionRate },
            { "OcclusionThreshold", result.OcclusionThreshold },
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
            { "Method", "MLS_Deformable" },
            { "FailureReason", reason },
            { "VerificationPassed", false },
            { "MatchCount", 0 },
            { "InlierCount", 0 },
            { "InlierRatio", 0.0 },
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
            { "Method", match.IsFallback ? "Rigid_Fallback" : "MLS_Deformable" },
            { "DeformationModel", match.IsFallback ? "RigidHomography" : "MovingLeastSquaresSimilarity" },
            { "Score", match.Score },
            { "OcclusionRate", match.OcclusionRate },
            { "OcclusionThreshold", match.OcclusionThreshold },
            { "DeformationMagnitude", match.DeformationMagnitude },
            { "CandidateScore", match.CandidateScore },
            { "VerificationScore", match.VerificationScore },
            { "VerificationPassed", match.VerificationPassed || match.IsFallback },
            { "InlierCount", match.InlierCount },
            { "InlierRatio", match.InlierRatio },
            { "IsFallback", match.IsFallback },
            { "FailureReason", match.FailureReason },
            { "LegacyParameterCompatibility", CreateLegacyParameterCompatibilityPayload(match) },
            { "BoundingBox", new { boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height } },
            { "Corners", match.Corners?.Select(point => new Position(point.X, point.Y)).ToList() ?? new List<Position>() }
        };
    }

    private static Dictionary<string, object> CreateLegacyParameterCompatibilityPayload(DeformableMatchResult match)
    {
        return new Dictionary<string, object>
        {
            { "TPSLambda", $"Applied as the MLS smoothing/regularization weight ({match.MlsLambda:F4})." },
            { "TPSGridSize", $"Applied as the MLS control grid dimension ({match.ControlGridSize})." }
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
        // 缁樺埗鎺у埗鐐硅繛绾?
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

    private static string BuildTemplateCacheKey(string path, int pyramidLevels)
    {
        var fingerprint = ComputeFileFingerprint(path);
        return $"{path}|{fingerprint}|{pyramidLevels}";
    }

    private static string ComputeFileFingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static void TouchTemplateCacheEntry(string cacheKey, TemplateData entry)
    {
        if (entry.OrderNode != null)
        {
            TemplateCacheOrder.Remove(entry.OrderNode);
        }

        entry.OrderNode = TemplateCacheOrder.AddLast(cacheKey);
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

    private sealed class TemplateData : IDisposable
    {
        public Mat BaseImage { get; set; } = new Mat();
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PyramidLevel> Pyramid { get; set; } = new();
        public LinkedListNode<string>? OrderNode { get; set; }

        public TemplateData Clone()
        {
            return new TemplateData
            {
                BaseImage = BaseImage.Clone(),
                Width = Width,
                Height = Height,
                Pyramid = Pyramid.Select(level => level.Clone()).ToList()
            };
        }

        public void Dispose()
        {
            BaseImage.Dispose();
            foreach (var level in Pyramid)
            {
                level.Dispose();
            }
        }
    }

    private sealed class PyramidLevel : IDisposable
    {
        public Mat Image { get; set; } = new Mat();
        public Mat Descriptors { get; set; } = new Mat();
        public KeyPoint[] KeyPoints { get; set; } = Array.Empty<KeyPoint>();
        public int Width { get; set; }
        public int Height { get; set; }
        public double Scale { get; set; }

        public PyramidLevel Clone()
        {
            return new PyramidLevel
            {
                Image = Image.Clone(),
                Descriptors = Descriptors.Clone(),
                KeyPoints = KeyPoints.ToArray(),
                Width = Width,
                Height = Height,
                Scale = Scale
            };
        }

        public void Dispose()
        {
            Image.Dispose();
            Descriptors.Dispose();
        }
    }

    private sealed class DeformableMatchResult : IDisposable
    {
        public bool IsSuccess { get; set; }
        public bool IsFallback { get; set; }
        public bool VerificationPassed { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public double Score { get; set; }
        public double CandidateScore { get; set; }
        public double VerificationScore { get; set; }
        public int InlierCount { get; set; }
        public double InlierRatio { get; set; }
        public int ControlGridSize { get; set; }
        public double MlsLambda { get; set; }
        public double OcclusionRate { get; set; }
        public double OcclusionThreshold { get; set; }
        public double DeformationMagnitude { get; set; }
        public Rect BoundingBox { get; set; }
        public Point2f[]? Corners { get; set; }
        public Point2f[]? ControlPoints { get; set; }
        public Point2f[]? DeformedPoints { get; set; }
        public Mat? Homography { get; set; }
        public Mat? OcclusionMask { get; set; }
        public RigidFallbackResult? RigidFallbackResult { get; set; }

        public void Dispose()
        {
            Homography?.Dispose();
            OcclusionMask?.Dispose();
            RigidFallbackResult?.Dispose();
        }
    }

    private sealed class RigidFallbackResult : IDisposable
    {
        public Mat Homography { get; set; } = new Mat();
        public Point2f[] Corners { get; set; } = Array.Empty<Point2f>();
        public bool VerificationPassed { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public int InlierCount { get; set; }
        public double InlierRatio { get; set; }
        public double VerificationScore { get; set; }
        public double Score { get; set; }

        public void Dispose()
        {
            Homography.Dispose();
        }
    }

    private readonly record struct CandidateWindow(Rect Roi, Rect Proposal, double Score);
}


