using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Shape Matching",
    Description = "Rotation-robust template matching with pyramid coarse-to-fine search.",
    Category = "Matching",
    IconName = "shape-match"
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
[OperatorParam("NumLevels", "Pyramid Levels", "int", DefaultValue = 3, Min = 1, Max = 6)]
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

                    List<MatchResult> levelMatches = new();
                    var candidateLimit = Math.Max(maxMatches * 5, 20);

                    for (var level = levelsUsed - 1; level >= 0; level--)
                    {
                        var levelSrc = srcPyramid[level];
                        var levelTmpl = tmplPyramid[level];
                        levelMatches = MatchByAngles(levelSrc, levelTmpl, currentAngles, minScore, candidateLimit);

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
                        { "Angle", m.Angle },
                        { "Score", m.Score },
                        { "CenterX", m.X + (m.Width / 2.0) },
                        { "CenterY", m.Y + (m.Height / 2.0) },
                        { "Width", m.Width },
                        { "Height", m.Height }
                    }).ToList();

                    var additionalData = new Dictionary<string, object>
                    {
                        { "Matches", matchResults },
                        { "MatchCount", matchResults.Count },
                        { "NumLevelsUsed", levelsUsed }
                    };

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

    private List<MatchResult> MatchByAngles(
        Mat srcGray,
        Mat tmplGray,
        IReadOnlyCollection<double> angles,
        double minScore,
        int candidateLimit)
    {
        var matches = new List<MatchResult>();
        var locker = new object();

        Parallel.ForEach(angles, angle =>
        {
            try
            {
                using var rotatedTemplate = RotateImage(tmplGray, angle);
                if (rotatedTemplate.Width >= srcGray.Width || rotatedTemplate.Height >= srcGray.Height)
                {
                    return;
                }

                using var matchResult = new Mat();
                Cv2.MatchTemplate(srcGray, rotatedTemplate, matchResult, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(matchResult, out _, out var maxVal, out _, out var maxLoc);

                if (maxVal < minScore)
                {
                    return;
                }

                var candidate = new MatchResult
                {
                    X = maxLoc.X,
                    Y = maxLoc.Y,
                    Angle = angle,
                    Score = maxVal,
                    Width = rotatedTemplate.Width,
                    Height = rotatedTemplate.Height
                };

                lock (locker)
                {
                    matches.Add(candidate);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Shape matching failed at angle {Angle}.", angle);
            }
        });

        return NonMaximumSuppression(matches, 0.4f)
            .Take(candidateLimit)
            .ToList();
    }

    private static Mat RotateImage(Mat src, double angle)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        using var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var rotated = new Mat();
        Cv2.WarpAffine(src, rotated, rotMatrix, src.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return rotated;
    }

    private static List<MatchResult> NonMaximumSuppression(List<MatchResult> matches, float iouThreshold)
    {
        var sorted = matches.OrderByDescending(m => m.Score).ToList();
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
            $"{match.Score:F2}@{match.Angle:F1}",
            new Point(match.X, Math.Max(12, match.Y - 6)),
            HersheyFonts.HersheySimplex,
            0.45,
            new Scalar(255, 0, 0),
            1);
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
        public double Angle { get; init; }
        public double Score { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }
}
