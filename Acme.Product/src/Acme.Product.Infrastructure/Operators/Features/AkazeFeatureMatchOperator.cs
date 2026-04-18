using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "AKAZE特征匹配",
    Description = "AKAZE feature matching with verified homography gating for robust template localization.",
    Category = "匹配定位",
    IconName = "feature-match"
)]
[InputPort("Image", "搜索图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Position", "匹配位置", PortDataType.Point)]
[OutputPort("MatchPoint", "代表匹配点", PortDataType.Point)]
[OutputPort("IsMatch", "是否匹配", PortDataType.Boolean)]
[OutputPort("Score", "匹配分数", PortDataType.Float)]
[OperatorParam("TemplatePath", "模板路径", "file", DefaultValue = "")]
[OperatorParam("Threshold", "检测阈值", "double", DefaultValue = 0.001, Min = 0.0001, Max = 0.1)]
[OperatorParam("MinMatchCount", "最小匹配数", "int", DefaultValue = 10, Min = 3, Max = 100)]
[OperatorParam("EnableSymmetryTest", "对称测试", "bool", DefaultValue = true)]
[OperatorParam("MaxFeatures", "最大特征点", "int", DefaultValue = 500, Min = 100, Max = 2000)]
[OperatorParam("OriginMode", "Origin Mode", "enum", DefaultValue = "Center", Options = new[] { "Center|Center", "TopLeft|TopLeft", "Custom|Custom" })]
[OperatorParam("OriginX", "Origin X", "double", DefaultValue = 0.0)]
[OperatorParam("OriginY", "Origin Y", "double", DefaultValue = 0.0)]
public class AkazeFeatureMatchOperator : FeatureMatchOperatorBase
{
    public override OperatorType OperatorType => OperatorType.AkazeFeatureMatch;

    public AkazeFeatureMatchOperator(ILogger<AkazeFeatureMatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像。"));
        }

        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var threshold = GetDoubleParam(@operator, "Threshold", 0.001, min: 0.0001, max: 0.1);
        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10, min: 3, max: 100);
        var enableSymmetryTest = GetBoolParam(@operator, "EnableSymmetryTest", true);
        var maxFeatures = GetIntParam(@operator, "MaxFeatures", 500, min: 100, max: 2000);

        var srcImage = imageWrapper.GetMat();
        using var srcGray = ToGray(srcImage);
        using var akaze = AKAZE.Create(threshold: (float)threshold);

        var srcDescriptors = new Mat();
        akaze.DetectAndCompute(srcGray, null, out KeyPoint[] srcKeyPoints, srcDescriptors);
        if (srcKeyPoints.Length < 4 || srcDescriptors.Empty())
        {
            srcDescriptors.Dispose();
            return Task.FromResult(CreateFailedOutput(srcImage, "场景特征点不足。", 0, 0));
        }

        Mat? templateImage = null;
        Mat? templateDescriptors = null;
        KeyPoint[]? templateKeyPoints = null;
        var disposeTemplateImage = false;

        try
        {
            if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
            {
                templateImage = templateWrapper.GetMat();
                using var templateGray = ToGray(templateImage);
                templateDescriptors = new Mat();
                akaze.DetectAndCompute(templateGray, null, out templateKeyPoints, templateDescriptors);
            }
            else if (!string.IsNullOrWhiteSpace(templatePath))
            {
                var cached = GetOrLoadTemplate(
                    templatePath,
                    $"AKAZE:{threshold:F6}:{maxFeatures}",
                    templateGray =>
                    {
                        var descriptors = new Mat();
                        akaze.DetectAndCompute(templateGray, null, out KeyPoint[] keyPoints, descriptors);
                        return (keyPoints, descriptors);
                    });

                if (cached.HasValue)
                {
                    (templateImage, templateKeyPoints, templateDescriptors) = cached.Value;
                    disposeTemplateImage = true;
                }
            }

            if (templateImage == null || templateKeyPoints == null || templateKeyPoints.Length < 4 || templateDescriptors == null || templateDescriptors.Empty())
            {
                return Task.FromResult(CreateFailedOutput(srcImage, "模板特征点不足。", 0, 0));
            }

            var (filteredTemplateKeyPoints, filteredTemplateDescriptors) = FilterFeatures(templateKeyPoints, templateDescriptors, maxFeatures);
            templateDescriptors.Dispose();
            templateDescriptors = filteredTemplateDescriptors;
            templateKeyPoints = filteredTemplateKeyPoints;

            List<DMatch> goodMatches;
            if (enableSymmetryTest)
            {
                goodMatches = MatchWithSymmetryTest(templateDescriptors, srcDescriptors);
            }
            else
            {
                using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
                var matches = matcher.KnnMatch(templateDescriptors, srcDescriptors, k: 2);
                goodMatches = new List<DMatch>();
                foreach (var match in matches)
                {
                    if (match.Length >= 2 && match[0].Distance < 0.75 * match[1].Distance)
                    {
                        goodMatches.Add(match[0]);
                    }
                }
            }

            var (homography, corners, metrics) = EstimateAndVerifyHomography(
                templateKeyPoints,
                srcKeyPoints,
                goodMatches,
                new Size(templateImage.Width, templateImage.Height),
                srcImage.Size(),
                ransacThreshold: 5.0,
                minMatchCount,
                minInliers: minMatchCount,
                minInlierRatio: 0.25);
            var origin = ResolveReferenceOrigin(@operator, templateImage.Size());

            var verificationScore = HomographyVerificationHelper.ComputeVerificationScore(metrics, 5.0);
            var isMatch = metrics.VerificationPassed;
            var resultImage = srcImage.Clone();
            var boxColor = isMatch ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

            if (homography != null && !homography.Empty() && isMatch)
            {
                DrawPerspectiveBox(resultImage, homography, templateImage.Width, templateImage.Height, boxColor);
            }

            var representativePoint = new Point(resultImage.Width / 2, resultImage.Height / 2);
            if (goodMatches.Count > 0)
            {
                var bestMatch = goodMatches[0];
                representativePoint = new Point(
                    (int)srcKeyPoints[bestMatch.TrainIdx].Pt.X,
                    (int)srcKeyPoints[bestMatch.TrainIdx].Pt.Y);
                Cv2.DrawMarker(resultImage, representativePoint, boxColor, MarkerTypes.Cross, 20, 2);
            }

            var position = TryProjectReferencePoint(homography, origin, corners, out var projectedCenter)
                ? projectedCenter
                : new Position(representativePoint.X, representativePoint.Y);

            Cv2.PutText(
                resultImage,
                $"{(isMatch ? "OK" : "NG")}: Inliers={metrics.InlierCount}/{metrics.MatchCount}",
                new Point(10, 30),
                HersheyFonts.HersheySimplex,
                0.6,
                boxColor,
                2);

            if (!isMatch && !string.IsNullOrWhiteSpace(metrics.FailureReason))
            {
                Cv2.PutText(
                    resultImage,
                    metrics.FailureReason,
                    new Point(10, 60),
                    HersheyFonts.HersheySimplex,
                    0.5,
                    boxColor,
                    2);
            }

            homography?.Dispose();

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
            {
                { "IsMatch", isMatch },
                { "Score", verificationScore },
                { "Inliers", metrics.InlierCount },
                { "TotalMatches", metrics.MatchCount },
                { "Position", position },
                { "MatchPoint", new Position(representativePoint.X, representativePoint.Y) },
                { "X", position.X },
                { "Y", position.Y },
                { "ScoreDefinition", "HomographyVerificationScore" },
                { "FailureReason", metrics.FailureReason },
                { "MeanReprojectionError", metrics.MeanReprojectionError },
                { "MaxReprojectionError", metrics.MaxReprojectionError },
                { "OriginMode", GetStringParam(@operator, "OriginMode", "Center") }
            })));
        }
        finally
        {
            srcDescriptors.Dispose();
            templateDescriptors?.Dispose();
            if (disposeTemplateImage)
            {
                templateImage?.Dispose();
            }
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 0.001);
        if (threshold < 0.0001 || threshold > 0.1)
        {
            return ValidationResult.Invalid("检测阈值必须在 0.0001-0.1 之间。");
        }

        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10);
        if (minMatchCount < 3 || minMatchCount > 100)
        {
            return ValidationResult.Invalid("最小匹配数必须在 3-100 之间。");
        }

        return ValidationResult.Valid();
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

    private OperatorExecutionOutput CreateFailedOutput(Mat input, string reason, int inliers, int totalMatches)
    {
        var output = input.Clone();
        Cv2.PutText(output, $"NG: {reason}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);
        Cv2.PutText(output, $"Score: {inliers}/{totalMatches}", new Point(10, 60), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(output, new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "Score", 0.0 },
            { "Inliers", inliers },
            { "TotalMatches", totalMatches },
            { "Message", reason },
            { "FailureReason", reason },
            { "Position", new Position(0, 0) },
            { "MatchPoint", new Position(0, 0) },
            { "X", 0 },
            { "Y", 0 },
            { "ScoreDefinition", "HomographyVerificationScore" }
        }));
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

    private static bool TryProjectReferencePoint(Mat? homography, Position origin, Point2f[] projectedCorners, out Position center)
    {
        center = new Position(0, 0);
        if (homography != null && !homography.Empty())
        {
            var projected = Cv2.PerspectiveTransform(new[] { new Point2f((float)origin.X, (float)origin.Y) }, homography);
            if (projected.Length == 1)
            {
                center = new Position(projected[0].X, projected[0].Y);
                return true;
            }
        }

        if (projectedCorners.Length != 4)
        {
            return false;
        }

        center = new Position(projectedCorners.Average(point => point.X), projectedCorners.Average(point => point.Y));
        return true;
    }
}
