// ShapeMatchingOperator.cs
// 形状匹配算子
// 对目标轮廓与模板轮廓进行匹配评分
// 作者：蘅芜君
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "旋转尺度模板匹配",
    Description = "Rotation-scale template matching with pyramid coarse-to-fine search. This is not a generic contour-descriptor matcher.",
    Category = "Matching",
    IconName = "shape-match",
    Version = "1.2.0"
)]
[InputPort("Image", "Search Image", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "Template Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("Matches", "Matches", PortDataType.Any)]
[OperatorParam("TemplatePath", "Template Path", "file", DefaultValue = "")]
[OperatorParam("MinScore", "Min Score", "double", DefaultValue = 0.7, Min = 0.1, Max = 1.0)]
[OperatorParam("MaxMatches", "Max Matches", "int", DefaultValue = 1, Min = 1, Max = 50)]
[OperatorParam("AngleStart", "Angle Start", "double", DefaultValue = -30.0, Min = -180.0, Max = 180.0)]
[OperatorParam("AngleExtent", "Angle Extent", "double", DefaultValue = 60.0, Min = 0.0, Max = 360.0)]
[OperatorParam("AngleStep", "Angle Step", "double", DefaultValue = 1.0, Min = 0.1, Max = 10.0)]
[OperatorParam("ScaleMin", "Scale Min", "double", DefaultValue = 1.0, Min = 0.2, Max = 3.0)]
[OperatorParam("ScaleMax", "Scale Max", "double", DefaultValue = 1.0, Min = 0.2, Max = 3.0)]
[OperatorParam("ScaleStep", "Scale Step", "double", DefaultValue = 0.1, Min = 0.01, Max = 1.0)]
[OperatorParam("NumLevels", "Pyramid Levels", "int", DefaultValue = 3, Min = 1, Max = 6)]
[OperatorParam("OriginMode", "Origin Mode", "enum", DefaultValue = "Center", Options = new[] { "Center|Center", "TopLeft|TopLeft", "Custom|Custom" })]
[OperatorParam("OriginX", "Origin X", "double", DefaultValue = 0.0)]
[OperatorParam("OriginY", "Origin Y", "double", DefaultValue = 0.0)]
public class ShapeMatchingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ShapeMatching;

    public ShapeMatchingOperator(ILogger<ShapeMatchingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Search image is required."));
        }

        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var minScore = GetDoubleParam(@operator, "MinScore", 0.7, min: 0.1, max: 1.0);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 1, min: 1, max: 50);
        var angleStart = GetDoubleParam(@operator, "AngleStart", -30.0, min: -180.0, max: 180.0);
        var angleExtent = GetDoubleParam(@operator, "AngleExtent", 60.0, min: 0.0, max: 360.0);
        var angleStep = GetDoubleParam(@operator, "AngleStep", 1.0, min: 0.1, max: 10.0);
        var scaleMin = GetDoubleParam(@operator, "ScaleMin", 1.0, min: 0.2, max: 3.0);
        var scaleMax = GetDoubleParam(@operator, "ScaleMax", 1.0, min: 0.2, max: 3.0);
        var scaleStep = GetDoubleParam(@operator, "ScaleStep", 0.1, min: 0.01, max: 1.0);
        var numLevels = GetIntParam(@operator, "NumLevels", 3, min: 1, max: 6);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Search image is invalid."));
        }

        Mat? templateMat = null;
        var shouldDisposeTemplate = false;

        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateMat = templateWrapper.GetMat();
        }
        else if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
        {
            templateMat = Cv2.ImRead(templatePath, ImreadModes.Color);
            shouldDisposeTemplate = true;
        }

        if (templateMat == null || templateMat.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Template image is required (Template input or TemplatePath)."));
        }

        try
        {
            return RunCpuBoundWork(() =>
            {
                using var srcGray = ToGray(src);
                using var tmplGray = ToGray(templateMat);
                var origin = ResolveReferenceOrigin(@operator, tmplGray.Size());

                if (!HasSufficientSignal(tmplGray))
                {
                    return CreateNoMatchOutput(src, "Template contains insufficient texture for stable matching.");
                }

                var (srcPyramid, tmplPyramid) = BuildPyramids(srcGray, tmplGray, numLevels);
                try
                {
                    var levelsUsed = srcPyramid.Count;
                    if (levelsUsed == 0)
                    {
                        return OperatorExecutionOutput.Failure("Pyramid construction failed.");
                    }

                    var angleEnd = angleStart + angleExtent;
                    var currentAngles = BuildAngleRange(
                        angleStart,
                        angleEnd,
                        ComputeLevelAngleStep(angleStep, levelsUsed - 1));
                    var currentScales = BuildScaleRange(
                        scaleMin,
                        scaleMax,
                        ComputeLevelScaleStep(scaleStep, levelsUsed - 1));

                    List<MatchResult> levelMatches = new();
                    var candidateLimit = Math.Max(maxMatches * 5, 20);

                    for (var level = levelsUsed - 1; level >= 0; level--)
                    {
                        var levelSrc = srcPyramid[level];
                        var levelTmpl = tmplPyramid[level];
                        levelMatches = MatchByTransforms(levelSrc, levelTmpl, currentAngles, currentScales, minScore, candidateLimit, origin);

                        if (level == 0)
                        {
                            break;
                        }

                        var nextStep = ComputeLevelAngleStep(angleStep, level - 1);
                        currentAngles = BuildRefinedAngles(levelMatches, angleStart, angleEnd, nextStep);
                        if (currentAngles.Count == 0)
                        {
                            currentAngles = BuildAngleRange(angleStart, angleEnd, nextStep);
                        }

                        var nextScaleStep = ComputeLevelScaleStep(scaleStep, level - 1);
                        currentScales = BuildRefinedScales(levelMatches, scaleMin, scaleMax, nextScaleStep);
                        if (currentScales.Count == 0)
                        {
                            currentScales = BuildScaleRange(scaleMin, scaleMax, nextScaleStep);
                        }
                    }

                    var finalMatches = NonMaximumSuppression(levelMatches, 0.5f)
                        .Take(maxMatches)
                        .ToList();

                    var resultImage = src.Clone();
                    foreach (var match in finalMatches)
                    {
                        DrawMatchResult(resultImage, match);
                    }

                    var matchResults = finalMatches.Select(m => new Dictionary<string, object>
                    {
                        { "X", m.X },
                        { "Y", m.Y },
                        { "XSubpixel", m.SubpixelX },
                        { "YSubpixel", m.SubpixelY },
                        { "Angle", m.Angle },
                        { "Scale", m.Scale },
                        { "Score", m.Score },
                        { "CenterX", m.SubpixelX + m.CenterOffsetX },
                        { "CenterY", m.SubpixelY + m.CenterOffsetY },
                        { "ReferenceX", m.SubpixelX + m.ReferenceOffsetX },
                        { "ReferenceY", m.SubpixelY + m.ReferenceOffsetY },
                        { "Width", m.Width },
                        { "Height", m.Height }
                    }).ToList();

                    var additionalData = new Dictionary<string, object>
                    {
                        { "IsMatch", matchResults.Count > 0 },
                        { "Score", matchResults.Count > 0 ? finalMatches[0].Score : 0.0 },
                        { "Method", "RotationScaleTemplateSearch" },
                        { "MaskAwareTransforms", true },
                        { "FailureReason", string.Empty },
                        { "Matches", matchResults },
                        { "MatchCount", matchResults.Count },
                        { "NumLevelsUsed", levelsUsed },
                        { "OriginMode", GetStringParam(@operator, "OriginMode", "Center") }
                    };

                    if (matchResults.Count > 0)
                    {
                        additionalData["Position"] = new Position(
                            Convert.ToDouble(matchResults[0]["ReferenceX"]),
                            Convert.ToDouble(matchResults[0]["ReferenceY"]));
                    }

                    if (matchResults.Count == 0)
                    {
                        additionalData["FailureReason"] = "No rotation-scale template match satisfied the score threshold.";
                    }

                    return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
                }
                finally
                {
                    foreach (var mat in srcPyramid)
                    {
                        mat.Dispose();
                    }

                    foreach (var mat in tmplPyramid)
                    {
                        mat.Dispose();
                    }
                }
            }, cancellationToken);
        }
        finally
        {
            if (shouldDisposeTemplate)
            {
                templateMat.Dispose();
            }
        }
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private Position ResolveReferenceOrigin(Operator @operator, Size templateSize)
    {
        var originMode = GetStringParam(@operator, "OriginMode", "Center");
        return originMode.Trim().ToLowerInvariant() switch
        {
            "topleft" => new Position(0, 0),
            "custom" => new Position(
                GetDoubleParam(@operator, "OriginX", 0.0),
                GetDoubleParam(@operator, "OriginY", 0.0)),
            _ => new Position(templateSize.Width / 2.0, templateSize.Height / 2.0)
        };
    }

    private static (List<Mat> srcPyramid, List<Mat> tmplPyramid) BuildPyramids(Mat srcGray, Mat tmplGray, int requestedLevels)
    {
        var srcPyramid = new List<Mat> { srcGray.Clone() };
        var tmplPyramid = new List<Mat> { tmplGray.Clone() };

        for (var i = 1; i < requestedLevels; i++)
        {
            using var nextSrc = new Mat();
            using var nextTmpl = new Mat();
            Cv2.PyrDown(srcPyramid[i - 1], nextSrc);
            Cv2.PyrDown(tmplPyramid[i - 1], nextTmpl);

            if (nextSrc.Width < 8 || nextSrc.Height < 8 || nextTmpl.Width < 4 || nextTmpl.Height < 4)
            {
                break;
            }

            if (nextTmpl.Width >= nextSrc.Width || nextTmpl.Height >= nextSrc.Height)
            {
                break;
            }

            srcPyramid.Add(nextSrc.Clone());
            tmplPyramid.Add(nextTmpl.Clone());
        }

        return (srcPyramid, tmplPyramid);
    }

    private static double ComputeLevelAngleStep(double baseStep, int level)
    {
        return Math.Clamp(baseStep * Math.Pow(2, level), baseStep, 90.0);
    }

    private static double ComputeLevelScaleStep(double baseStep, int level)
    {
        return Math.Clamp(baseStep * Math.Pow(2, level), baseStep, 1.0);
    }

    private static List<double> BuildAngleRange(double start, double end, double step)
    {
        var safeStep = Math.Max(0.1, step);
        var angles = new List<double>();

        if (end < start)
        {
            (start, end) = (end, start);
        }

        for (var angle = start; angle <= end + 1e-9; angle += safeStep)
        {
            angles.Add(Math.Round(angle, 4));
        }

        if (angles.Count == 0)
        {
            angles.Add(Math.Round(start, 4));
        }

        return angles;
    }

    private static List<double> BuildScaleRange(double minScale, double maxScale, double step)
    {
        var safeStep = Math.Max(0.01, step);
        if (maxScale < minScale)
        {
            (minScale, maxScale) = (maxScale, minScale);
        }

        var scales = new List<double>();
        for (var scale = minScale; scale <= maxScale + 1e-9; scale += safeStep)
        {
            scales.Add(Math.Round(scale, 4));
        }

        if (scales.Count == 0)
        {
            scales.Add(Math.Round(minScale, 4));
        }

        return scales;
    }

    private static List<double> BuildRefinedAngles(
        IReadOnlyCollection<MatchResult> matches,
        double start,
        double end,
        double step)
    {
        var safeStep = Math.Max(0.1, step);
        if (matches.Count == 0)
        {
            return BuildAngleRange(start, end, safeStep);
        }

        var selected = matches
            .OrderByDescending(m => m.Score)
            .Take(8)
            .ToList();

        var values = new SortedSet<double>();
        foreach (var match in selected)
        {
            var localStart = Math.Max(start, match.Angle - (2 * safeStep));
            var localEnd = Math.Min(end, match.Angle + (2 * safeStep));
            for (var angle = localStart; angle <= localEnd + 1e-9; angle += safeStep)
            {
                values.Add(Math.Round(angle, 4));
            }
        }

        return values.ToList();
    }

    private static List<double> BuildRefinedScales(
        IReadOnlyCollection<MatchResult> matches,
        double minScale,
        double maxScale,
        double step)
    {
        var safeStep = Math.Max(0.01, step);
        if (matches.Count == 0)
        {
            return BuildScaleRange(minScale, maxScale, safeStep);
        }

        var selected = matches
            .OrderByDescending(m => m.Score)
            .Take(8)
            .ToList();

        var values = new SortedSet<double>();
        foreach (var match in selected)
        {
            var localStart = Math.Max(minScale, match.Scale - (2 * safeStep));
            var localEnd = Math.Min(maxScale, match.Scale + (2 * safeStep));
            for (var scale = localStart; scale <= localEnd + 1e-9; scale += safeStep)
            {
                values.Add(Math.Round(scale, 4));
            }
        }

        return values.ToList();
    }

    private List<MatchResult> MatchByTransforms(
        Mat srcGray,
        Mat tmplGray,
        IReadOnlyCollection<double> angles,
        IReadOnlyCollection<double> scales,
        double minScore,
        int candidateLimit,
        Position origin)
    {
        var matches = new List<MatchResult>();
        var locker = new object();
        var perTransformLimit = Math.Clamp(candidateLimit / 4, 2, 8);
        using var templateMask = new Mat(tmplGray.Size(), MatType.CV_8UC1, Scalar.All(255));

        var transforms = angles.SelectMany(angle => scales.Select(scale => (angle, scale))).ToList();
        Parallel.ForEach(transforms, transform =>
        {
            try
            {
                using var transformedTemplate = TransformTemplate(tmplGray, transform.angle, transform.scale, InterpolationFlags.Linear);
                using var transformedMask = TransformTemplate(templateMask, transform.angle, transform.scale, InterpolationFlags.Nearest);
                BinarizeMask(transformedMask);
                var geometry = ComputeTransformGeometry(tmplGray.Size(), transform.angle, transform.scale, origin);
                if (transformedTemplate.Width >= srcGray.Width || transformedTemplate.Height >= srcGray.Height)
                {
                    return;
                }

                var validPixelCount = Cv2.CountNonZero(transformedMask);
                if (validPixelCount <= 0)
                {
                    return;
                }

                var usesEffectiveRegionConstraint = validPixelCount < (transformedMask.Rows * transformedMask.Cols);
                using var matchResult = new Mat();
                if (usesEffectiveRegionConstraint)
                {
                    Cv2.MatchTemplate(srcGray, transformedTemplate, matchResult, TemplateMatchModes.CCorrNormed, transformedMask);
                }
                else
                {
                    Cv2.MatchTemplate(srcGray, transformedTemplate, matchResult, TemplateMatchModes.CCoeffNormed);
                }

                SanitizeMatchTemplateResult(matchResult);
                var candidates = FindPeaksForTransform(
                    matchResult,
                    transformedTemplate.Size(),
                    transform.angle,
                    transform.scale,
                    geometry,
                    minScore,
                    perTransformLimit);
                candidates = ReScoreCandidates(
                    srcGray,
                    transformedTemplate,
                    usesEffectiveRegionConstraint ? transformedMask : null,
                    candidates,
                    minScore);

                lock (locker)
                {
                    matches.AddRange(candidates);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Shape matching failed at angle {Angle}, scale {Scale}.", transform.angle, transform.scale);
            }
        });

        return NonMaximumSuppression(matches, 0.4f)
            .Take(candidateLimit)
            .ToList();
    }

    private static void SanitizeMatchTemplateResult(Mat matchResult)
    {
        if (matchResult.Empty() || matchResult.Type() != MatType.CV_32FC1)
        {
            return;
        }

        var indexer = matchResult.GetGenericIndexer<float>();
        for (var row = 0; row < matchResult.Rows; row++)
        {
            for (var col = 0; col < matchResult.Cols; col++)
            {
                var value = indexer[row, col];
                if (!float.IsNaN(value) && !float.IsInfinity(value))
                {
                    continue;
                }

                indexer[row, col] = -1f;
            }
        }
    }

    private static bool TryRefineSubpixelPeak(Mat matchResult, Point maxLoc, out double dx, out double dy)
    {
        dx = 0.0;
        dy = 0.0;

        // matchResult is CV_32FC1 for MatchTemplate. Guard just in case.
        if (matchResult.Empty() || matchResult.Cols < 3 || matchResult.Rows < 3)
        {
            return false;
        }

        var x = maxLoc.X;
        var y = maxLoc.Y;

        // Need a full 3x3 neighborhood for a stable 2D quadratic refinement.
        if (x <= 0 || x >= matchResult.Cols - 1 || y <= 0 || y >= matchResult.Rows - 1)
        {
            return false;
        }

        // Prefer a 5x5 quadratic least-squares fit when we have room. It is more accurate than
        // simple finite differences when the correlation surface deviates slightly from an ideal
        // paraboloid due to interpolation and sampling.
        //
        // Model (local coordinates):
        //   f(x,y) = a*x^2 + b*y^2 + c*x*y + d*x + e*y + g
        // Peak satisfies:
        //   [2a  c] [x] = [-d]
        //   [ c 2b] [y]   [-e]
        if (x >= 2 && x <= matchResult.Cols - 3 && y >= 2 && y <= matchResult.Rows - 3)
        {
            // Build normal equations A * p = b for p=[a,b,c,d,e,g].
            // A is 6x6, b is 6. We solve via Gaussian elimination with partial pivoting.
            Span<double> A = stackalloc double[36];
            Span<double> b = stackalloc double[6];
            Span<double> r = stackalloc double[6];

            for (int j = -2; j <= 2; j++)
            {
                for (int i = -2; i <= 2; i++)
                {
                    double xi = i;
                    double yi = j;
                    double v = matchResult.At<float>(y + j, x + i);

                    double r0 = xi * xi;
                    double r1 = yi * yi;
                    double r2 = xi * yi;
                    double r3 = xi;
                    double r4 = yi;
                    double r5 = 1.0;

                    // r = [r0..r5]
                    r[0] = r0;
                    r[1] = r1;
                    r[2] = r2;
                    r[3] = r3;
                    r[4] = r4;
                    r[5] = r5;

                    for (int row = 0; row < 6; row++)
                    {
                        b[row] += r[row] * v;
                        for (int col = 0; col < 6; col++)
                        {
                            A[(row * 6) + col] += r[row] * r[col];
                        }
                    }
                }
            }

            Span<double> p = stackalloc double[6];
            if (TrySolveLinearSystem6x6(A, b, p))
            {
                double a = p[0];
                double bb = p[1];
                double c = p[2];
                double d = p[3];
                double e = p[4];

                double det = (2.0 * a * 2.0 * bb) - (c * c);
                if (Math.Abs(det) > 1e-12)
                {
                    dx = ((c * e) - (2.0 * bb * d)) / det;
                    dy = ((c * d) - (2.0 * a * e)) / det;

                    if (!double.IsNaN(dx) && !double.IsInfinity(dx) && !double.IsNaN(dy) && !double.IsInfinity(dy))
                    {
                        dx = Math.Clamp(dx, -1.0, 1.0);
                        dy = Math.Clamp(dy, -1.0, 1.0);
                        return true;
                    }
                }
            }
        }

        // Fallback: 3x3 finite-difference Hessian solve (fast and allocation-free).
        double f00 = matchResult.At<float>(y, x);
        double f10 = matchResult.At<float>(y, x + 1);
        double fm10 = matchResult.At<float>(y, x - 1);
        double f01 = matchResult.At<float>(y + 1, x);
        double f0m1 = matchResult.At<float>(y - 1, x);
        double f11 = matchResult.At<float>(y + 1, x + 1);
        double f1m1 = matchResult.At<float>(y - 1, x + 1);
        double fm11 = matchResult.At<float>(y + 1, x - 1);
        double fm1m1 = matchResult.At<float>(y - 1, x - 1);

        double fx = (f10 - fm10) * 0.5;
        double fy = (f01 - f0m1) * 0.5;
        double fxx = f10 - (2.0 * f00) + fm10;
        double fyy = f01 - (2.0 * f00) + f0m1;
        double fxy = (f11 - f1m1 - fm11 + fm1m1) * 0.25;

        double det2 = (fxx * fyy) - (fxy * fxy);
        if (Math.Abs(det2) < 1e-12)
        {
            return false;
        }

        dx = ((fxy * fy) - (fyy * fx)) / det2;
        dy = ((fxy * fx) - (fxx * fy)) / det2;

        if (double.IsNaN(dx) || double.IsInfinity(dx) || double.IsNaN(dy) || double.IsInfinity(dy))
        {
            dx = 0.0;
            dy = 0.0;
            return false;
        }

        dx = Math.Clamp(dx, -1.0, 1.0);
        dy = Math.Clamp(dy, -1.0, 1.0);

        return true;
    }

    private static List<MatchResult> FindPeaksForTransform(
        Mat matchResult,
        Size templateSize,
        double angle,
        double scale,
        TransformGeometry geometry,
        double minScore,
        int maxCandidates)
    {
        using var working = matchResult.Clone();
        var candidates = new List<MatchResult>();

        for (var index = 0; index < maxCandidates; index++)
        {
            Cv2.MinMaxLoc(working, out _, out var maxVal, out _, out var maxLoc);
            if (maxVal < minScore)
            {
                break;
            }

            var refinedX = (double)maxLoc.X;
            var refinedY = (double)maxLoc.Y;
            if (TryRefineSubpixelPeak(matchResult, maxLoc, out var dx, out var dy))
            {
                refinedX += dx;
                refinedY += dy;
            }

            candidates.Add(new MatchResult
            {
                X = maxLoc.X,
                Y = maxLoc.Y,
                SubpixelX = refinedX,
                SubpixelY = refinedY,
                Angle = angle,
                Scale = scale,
                Score = maxVal,
                Width = templateSize.Width,
                Height = templateSize.Height,
                CenterOffsetX = geometry.CenterOffset.X,
                CenterOffsetY = geometry.CenterOffset.Y,
                ReferenceOffsetX = geometry.ReferenceOffset.X,
                ReferenceOffsetY = geometry.ReferenceOffset.Y
            });

            SuppressPeakRegion(working, maxLoc, maxVal, minScore, templateSize);
        }

        return candidates;
    }

    private static List<MatchResult> ReScoreCandidates(
        Mat srcGray,
        Mat transformedTemplate,
        Mat? transformedMask,
        IReadOnlyCollection<MatchResult> candidates,
        double minScore)
    {
        if (candidates.Count == 0)
        {
            return new List<MatchResult>();
        }

        var rescored = new List<MatchResult>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var coarseScore = candidate.Score;
            if (double.IsNaN(coarseScore) || double.IsInfinity(coarseScore))
            {
                coarseScore = -1.0;
            }

            var verificationScore = ComputeVerificationScore(
                srcGray,
                transformedTemplate,
                transformedMask,
                candidate.X,
                candidate.Y);
            if (double.IsNaN(verificationScore) || double.IsInfinity(verificationScore))
            {
                verificationScore = -1.0;
            }

            var blendedScore = Math.Clamp(
                (coarseScore * 0.25) + (verificationScore * 0.75),
                -1.0,
                1.0);
            if (blendedScore < minScore)
            {
                continue;
            }

            rescored.Add(new MatchResult
            {
                X = candidate.X,
                Y = candidate.Y,
                SubpixelX = candidate.SubpixelX,
                SubpixelY = candidate.SubpixelY,
                Angle = candidate.Angle,
                Scale = candidate.Scale,
                Score = blendedScore,
                Width = candidate.Width,
                Height = candidate.Height,
                CenterOffsetX = candidate.CenterOffsetX,
                CenterOffsetY = candidate.CenterOffsetY,
                ReferenceOffsetX = candidate.ReferenceOffsetX,
                ReferenceOffsetY = candidate.ReferenceOffsetY
            });
        }

        return rescored;
    }

    private static double ComputeVerificationScore(
        Mat srcGray,
        Mat transformedTemplate,
        Mat? transformedMask,
        int x,
        int y)
    {
        if (x < 0 || y < 0 ||
            x + transformedTemplate.Width > srcGray.Width ||
            y + transformedTemplate.Height > srcGray.Height)
        {
            return double.NegativeInfinity;
        }

        using var roi = new Mat(srcGray, new Rect(x, y, transformedTemplate.Width, transformedTemplate.Height));
        if (transformedMask == null)
        {
            using var result = new Mat();
            Cv2.MatchTemplate(roi, transformedTemplate, result, TemplateMatchModes.CCoeffNormed);
            return result.At<float>(0, 0);
        }

        return ComputeMaskedZeroMeanCorrelation(roi, transformedTemplate, transformedMask);
    }

    private static double ComputeMaskedZeroMeanCorrelation(Mat roi, Mat template, Mat mask)
    {
        using var roi32 = new Mat();
        using var template32 = new Mat();
        roi.ConvertTo(roi32, MatType.CV_32FC1);
        template.ConvertTo(template32, MatType.CV_32FC1);

        var roiIndexer = roi32.GetGenericIndexer<float>();
        var templateIndexer = template32.GetGenericIndexer<float>();
        var maskIndexer = mask.GetGenericIndexer<byte>();

        double roiSum = 0.0;
        double templateSum = 0.0;
        double weight = 0.0;

        for (var row = 0; row < roi32.Rows; row++)
        {
            for (var col = 0; col < roi32.Cols; col++)
            {
                if (maskIndexer[row, col] == 0)
                {
                    continue;
                }

                roiSum += roiIndexer[row, col];
                templateSum += templateIndexer[row, col];
                weight += 1.0;
            }
        }

        if (weight <= 1.0)
        {
            return double.NegativeInfinity;
        }

        var roiMean = roiSum / weight;
        var templateMean = templateSum / weight;
        double numerator = 0.0;
        double roiEnergy = 0.0;
        double templateEnergy = 0.0;
        double rawNumerator = 0.0;
        double rawRoiEnergy = 0.0;
        double rawTemplateEnergy = 0.0;

        for (var row = 0; row < roi32.Rows; row++)
        {
            for (var col = 0; col < roi32.Cols; col++)
            {
                if (maskIndexer[row, col] == 0)
                {
                    continue;
                }

                var roiSample = roiIndexer[row, col];
                var templateSample = templateIndexer[row, col];
                var roiValue = roiSample - roiMean;
                var templateValue = templateSample - templateMean;
                numerator += roiValue * templateValue;
                roiEnergy += roiValue * roiValue;
                templateEnergy += templateValue * templateValue;
                rawNumerator += roiSample * templateSample;
                rawRoiEnergy += roiSample * roiSample;
                rawTemplateEnergy += templateSample * templateSample;
            }
        }

        if (roiEnergy <= 1e-9 || templateEnergy <= 1e-9)
        {
            // Uniform masked foregrounds legitimately carry pose via the mask support itself.
            // Fall back to masked raw correlation so rescoring does not discard coarse mask-aware hits.
            return ComputeNormalizedCorrelation(rawNumerator, rawRoiEnergy, rawTemplateEnergy);
        }

        return ComputeNormalizedCorrelation(numerator, roiEnergy, templateEnergy);
    }

    private static double ComputeNormalizedCorrelation(double numerator, double leftEnergy, double rightEnergy)
    {
        if (leftEnergy <= 1e-9 || rightEnergy <= 1e-9)
        {
            return double.NegativeInfinity;
        }

        return numerator / Math.Sqrt(leftEnergy * rightEnergy);
    }

    private static void SuppressPeakRegion(Mat working, Point peakLocation, double peakScore, double minScore, Size templateSize)
    {
        var suppressionFloor = Math.Max(minScore, peakScore - Math.Max(0.02, (peakScore - minScore) * 0.25));
        using var highResponseMask = new Mat();
        Cv2.Compare(working, new Scalar(suppressionFloor), highResponseMask, CmpType.GE);

        var paddingX = Math.Max(1, templateSize.Width / 4);
        var paddingY = Math.Max(1, templateSize.Height / 4);
        Rect suppressBounds;
        if (highResponseMask.At<byte>(peakLocation.Y, peakLocation.X) != 0)
        {
            using var floodMask = new Mat(highResponseMask.Rows + 2, highResponseMask.Cols + 2, MatType.CV_8UC1, Scalar.Black);
            Cv2.FloodFill(highResponseMask, floodMask, peakLocation, Scalar.All(128), out var componentBounds);
            suppressBounds = ExpandRect(componentBounds, paddingX, paddingY, working.Width, working.Height);
        }
        else
        {
            suppressBounds = ExpandRect(new Rect(peakLocation.X, peakLocation.Y, 1, 1), paddingX, paddingY, working.Width, working.Height);
        }

        if (suppressBounds.Width <= 0 || suppressBounds.Height <= 0)
        {
            working.Set(peakLocation.Y, peakLocation.X, 0f);
            return;
        }

        using var region = new Mat(working, suppressBounds);
        region.SetTo(0f);
    }

    private static Rect ExpandRect(Rect rect, int paddingX, int paddingY, int maxWidth, int maxHeight)
    {
        var left = Math.Max(0, rect.X - paddingX);
        var top = Math.Max(0, rect.Y - paddingY);
        var right = Math.Min(maxWidth, rect.Right + paddingX);
        var bottom = Math.Min(maxHeight, rect.Bottom + paddingY);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static bool TrySolveLinearSystem6x6(Span<double> A, Span<double> b, Span<double> x)
    {
        // Solve A*x=b using Gaussian elimination with partial pivoting.
        // Mutates A and b (callers should pass fresh arrays).
        const int n = 6;
        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            double pivotAbs = Math.Abs(A[(k * 6) + k]);
            for (int r = k + 1; r < n; r++)
            {
                double v = Math.Abs(A[(r * 6) + k]);
                if (v > pivotAbs)
                {
                    pivotAbs = v;
                    pivotRow = r;
                }
            }

            if (pivotAbs < 1e-12)
            {
                return false;
            }

            if (pivotRow != k)
            {
                for (int c = k; c < n; c++)
                {
                    int i1 = (k * 6) + c;
                    int i2 = (pivotRow * 6) + c;
                    (A[i1], A[i2]) = (A[i2], A[i1]);
                }
                (b[k], b[pivotRow]) = (b[pivotRow], b[k]);
            }

            double pivot = A[(k * 6) + k];
            for (int c = k; c < n; c++)
            {
                A[(k * 6) + c] /= pivot;
            }
            b[k] /= pivot;

            for (int r = 0; r < n; r++)
            {
                if (r == k) continue;
                double factor = A[(r * 6) + k];
                if (Math.Abs(factor) < 1e-18) continue;

                for (int c = k; c < n; c++)
                {
                    A[(r * 6) + c] -= factor * A[(k * 6) + c];
                }
                b[r] -= factor * b[k];
            }
        }

        for (int i = 0; i < n; i++)
        {
            x[i] = b[i];
        }

        return true;
    }

    private static Mat RotateImageExpanded(Mat src, double angle)
    {
        return RotateImageExpanded(src, angle, InterpolationFlags.Linear);
    }

    private static Mat RotateImageExpanded(Mat src, double angle, InterpolationFlags interpolation)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        using var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var cos = Math.Abs(rotMatrix.Get<double>(0, 0));
        var sin = Math.Abs(rotMatrix.Get<double>(0, 1));
        var boundWidth = Math.Max(1, (int)Math.Ceiling((src.Height * sin) + (src.Width * cos)));
        var boundHeight = Math.Max(1, (int)Math.Ceiling((src.Height * cos) + (src.Width * sin)));

        rotMatrix.Set(0, 2, rotMatrix.Get<double>(0, 2) + (boundWidth / 2.0) - center.X);
        rotMatrix.Set(1, 2, rotMatrix.Get<double>(1, 2) + (boundHeight / 2.0) - center.Y);

        var rotated = new Mat();
        Cv2.WarpAffine(
            src,
            rotated,
            rotMatrix,
            new Size(boundWidth, boundHeight),
            interpolation,
            BorderTypes.Constant,
            Scalar.Black);
        return rotated;
    }

    private static Mat TransformTemplate(Mat src, double angle, double scale, InterpolationFlags interpolation)
    {
        using var rotated = RotateImageExpanded(src, angle, interpolation);
        if (Math.Abs(scale - 1.0) < 1e-6)
        {
            return rotated.Clone();
        }

        var transformed = new Mat();
        Cv2.Resize(rotated, transformed, new Size(), scale, scale, interpolation);
        return transformed;
    }

    private static TransformGeometry ComputeTransformGeometry(Size sourceSize, double angle, double scale, Position origin)
    {
        var center = new Point2d(sourceSize.Width / 2.0, sourceSize.Height / 2.0);
        using var rotationMatrix = Cv2.GetRotationMatrix2D(new Point2f((float)center.X, (float)center.Y), angle, 1.0);
        var cos = Math.Abs(rotationMatrix.Get<double>(0, 0));
        var sin = Math.Abs(rotationMatrix.Get<double>(0, 1));
        var boundWidth = Math.Max(1, (int)Math.Ceiling((sourceSize.Height * sin) + (sourceSize.Width * cos)));
        var boundHeight = Math.Max(1, (int)Math.Ceiling((sourceSize.Height * cos) + (sourceSize.Width * sin)));

        rotationMatrix.Set(0, 2, rotationMatrix.Get<double>(0, 2) + (boundWidth / 2.0) - center.X);
        rotationMatrix.Set(1, 2, rotationMatrix.Get<double>(1, 2) + (boundHeight / 2.0) - center.Y);

        var scaledMatrix = new[]
        {
            new[] { rotationMatrix.Get<double>(0, 0) * scale, rotationMatrix.Get<double>(0, 1) * scale, rotationMatrix.Get<double>(0, 2) * scale },
            new[] { rotationMatrix.Get<double>(1, 0) * scale, rotationMatrix.Get<double>(1, 1) * scale, rotationMatrix.Get<double>(1, 2) * scale }
        };

        return new TransformGeometry(
            ApplyAffine(scaledMatrix, center),
            ApplyAffine(scaledMatrix, new Point2d(origin.X, origin.Y)));
    }

    private static Point2d ApplyAffine(IReadOnlyList<double[]> matrix, Point2d point)
    {
        return new Point2d(
            (matrix[0][0] * point.X) + (matrix[0][1] * point.Y) + matrix[0][2],
            (matrix[1][0] * point.X) + (matrix[1][1] * point.Y) + matrix[1][2]);
    }

    private static void BinarizeMask(Mat mask)
    {
        if (mask.Empty())
        {
            return;
        }

        Cv2.Threshold(mask, mask, 0, 255, ThresholdTypes.Binary);
    }

    private static List<MatchResult> NonMaximumSuppression(List<MatchResult> matches, float iouThreshold)
    {
        var sorted = matches
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Y)
            .ThenBy(m => m.X)
            .ThenBy(m => m.Angle)
            .ThenBy(m => m.Scale)
            .ToList();
        var result = new List<MatchResult>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);
            sorted = sorted.Where(m => CalculateIoU(best, m) < iouThreshold).ToList();
        }

        return result;
    }

    private static float CalculateIoU(MatchResult a, MatchResult b)
    {
        var rectA = new Rect(a.X, a.Y, a.Width, a.Height);
        var rectB = new Rect(b.X, b.Y, b.Width, b.Height);

        var intersection = Rect.Intersect(rectA, rectB);
        if (intersection.Width <= 0 || intersection.Height <= 0)
        {
            return 0f;
        }

        var interArea = intersection.Width * intersection.Height;
        var unionArea = (rectA.Width * rectA.Height) + (rectB.Width * rectB.Height) - interArea;
        return unionArea <= 0 ? 0f : (float)interArea / unionArea;
    }

    private static void DrawMatchResult(Mat image, MatchResult match)
    {
        var rect = new Rect(match.X, match.Y, match.Width, match.Height);
        Cv2.Rectangle(image, rect, new Scalar(0, 255, 0), 2);

        var center = new Point((int)(match.X + (match.Width / 2.0)), (int)(match.Y + (match.Height / 2.0)));
        Cv2.Circle(image, center, 4, new Scalar(0, 0, 255), -1);

        Cv2.PutText(
            image,
            $"{match.Score:F2}@{match.Angle:F1}/x{match.Scale:F2}",
            new Point(match.X, Math.Max(12, match.Y - 6)),
            HersheyFonts.HersheySimplex,
            0.45,
            new Scalar(255, 0, 0),
            1);
    }

    private static bool HasSufficientSignal(Mat image)
    {
        if (image.Empty())
        {
            return false;
        }

        var indexer = image.GetGenericIndexer<byte>();
        var firstValue = indexer[0, 0];
        for (var y = 0; y < image.Rows; y++)
        {
            for (var x = 0; x < image.Cols; x++)
            {
                if (indexer[y, x] != firstValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private OperatorExecutionOutput CreateNoMatchOutput(Mat sourceImage, string failureReason)
    {
        var resultImage = sourceImage.Clone();
        var additionalData = new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "Score", 0.0 },
            { "Method", "RotationScaleTemplateSearch" },
            { "MaskAwareTransforms", true },
            { "FailureReason", failureReason },
            { "Matches", Array.Empty<object>() },
            { "MatchCount", 0 },
            { "NumLevelsUsed", 0 }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetDoubleParam(@operator, "MinScore", 0.7);
        if (minScore < 0.1 || minScore > 1.0)
        {
            return ValidationResult.Invalid("MinScore must be between 0.1 and 1.0.");
        }

        var maxMatches = GetIntParam(@operator, "MaxMatches", 1);
        if (maxMatches < 1 || maxMatches > 50)
        {
            return ValidationResult.Invalid("MaxMatches must be between 1 and 50.");
        }

        var angleStep = GetDoubleParam(@operator, "AngleStep", 1.0);
        if (angleStep < 0.1 || angleStep > 10.0)
        {
            return ValidationResult.Invalid("AngleStep must be between 0.1 and 10.0.");
        }

        var scaleMin = GetDoubleParam(@operator, "ScaleMin", 1.0);
        var scaleMax = GetDoubleParam(@operator, "ScaleMax", 1.0);
        if (scaleMin < 0.2 || scaleMin > 3.0 || scaleMax < 0.2 || scaleMax > 3.0 || scaleMin > scaleMax)
        {
            return ValidationResult.Invalid("ScaleMin/ScaleMax must be within [0.2, 3.0] and ScaleMin <= ScaleMax.");
        }

        var scaleStep = GetDoubleParam(@operator, "ScaleStep", 0.1);
        if (scaleStep < 0.01 || scaleStep > 1.0)
        {
            return ValidationResult.Invalid("ScaleStep must be between 0.01 and 1.0.");
        }

        var numLevels = GetIntParam(@operator, "NumLevels", 3);
        if (numLevels < 1 || numLevels > 6)
        {
            return ValidationResult.Invalid("NumLevels must be between 1 and 6.");
        }

        return ValidationResult.Valid();
    }

    private sealed class MatchResult
    {
        public int X { get; init; }
        public int Y { get; init; }
        public double SubpixelX { get; init; }
        public double SubpixelY { get; init; }
        public double Angle { get; init; }
        public double Scale { get; init; } = 1.0;
        public double Score { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public double CenterOffsetX { get; init; }
        public double CenterOffsetY { get; init; }
        public double ReferenceOffsetX { get; init; }
        public double ReferenceOffsetY { get; init; }
    }

    private readonly record struct TransformGeometry(Point2d CenterOffset, Point2d ReferenceOffset);
}
