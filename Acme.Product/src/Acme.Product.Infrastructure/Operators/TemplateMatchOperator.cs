// TemplateMatchOperator.cs
// 模板匹配算子 - 在图像中查找模板位置
// 作者：ClearVision Team

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 模板匹配算子 - 在图像中查找模板位置。
/// </summary>
[OperatorMeta(
    DisplayName = "模板匹配",
    Description = "Classic grayscale template matching for fixed-scale, low-rotation scenes. Multi-match outputs are filtered by IoU-based NMS.",
    Category = "匹配定位",
    IconName = "template",
    Keywords = new[] { "模板匹配", "定位", "找图", "Template", "Match", "Locate" },
    Version = "1.1.1"
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = true)]
[InputPort("Mask", "搜索掩膜", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Position", "匹配位置", PortDataType.Point)]
[OutputPort("Score", "匹配分数", PortDataType.Float)]
[OutputPort("IsMatch", "是否匹配", PortDataType.Boolean)]
[OutputPort("Matches", "匹配列表", PortDataType.Any)]
[OutputPort("MatchCount", "匹配数量", PortDataType.Integer)]
[OperatorParam("Method", "匹配方法", "enum", DefaultValue = "CCoeffNormed", Options = new[]
{
    "CCoeffNormed|CCoeffNormed",
    "SqDiff|SqDiff",
    "SqDiffNormed|SqDiffNormed",
    "CCorr|CCorr",
    "CCorrNormed|CCorrNormed",
    "CCoeff|CCoeff"
})]
[OperatorParam("Domain", "匹配域", "enum", DefaultValue = "Gray", Options = new[]
{
    "Gray|Gray",
    "Edge|Edge",
    "Gradient|Gradient"
})]
[OperatorParam("Threshold", "匹配分数阈值", "double", DefaultValue = 0.8, Min = 0.0, Max = 1.0)]
[OperatorParam("MaxMatches", "最大匹配数", "int", DefaultValue = 1, Min = 1, Max = 100)]
[OperatorParam("UseRoi", "使用 ROI", "bool", DefaultValue = false)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiWidth", "ROI Width", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiHeight", "ROI Height", "int", DefaultValue = 0, Min = 0)]
public class TemplateMatchOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TemplateMatching;

    public TemplateMatchOperator(ILogger<TemplateMatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像。"));
        }

        if (!TryGetInputImage(inputs, "Template", out var templateWrapper) || templateWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像。"));
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 0.8, min: 0, max: 1);
        var method = GetStringParam(@operator, "Method", "CCoeffNormed");
        var domain = GetStringParam(@operator, "Domain", "Gray");
        var maxMatches = GetIntParam(@operator, "MaxMatches", 1, min: 1, max: 100);
        var useRoi = GetBoolParam(@operator, "UseRoi", false);
        var roiX = GetIntParam(@operator, "RoiX", 0);
        var roiY = GetIntParam(@operator, "RoiY", 0);
        var roiWidth = GetIntParam(@operator, "RoiWidth", 0);
        var roiHeight = GetIntParam(@operator, "RoiHeight", 0);

        var src = imageWrapper.GetMat();
        var template = templateWrapper.GetMat();

        if (src.Empty() || template.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码图像。"));
        }

        if (template.Width > src.Width || template.Height > src.Height)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("模板尺寸不能大于源图像。"));
        }

        Mat searchRegion = src;
        Rect roi = new Rect(0, 0, src.Width, src.Height);
        var disposeSearchRegion = false;
        if (useRoi && roiWidth > 0 && roiHeight > 0)
        {
            roi = new Rect(roiX, roiY, roiWidth, roiHeight);
            roi = roi.Intersect(new Rect(0, 0, src.Width, src.Height));
            if (roi.Width > 0 && roi.Height > 0)
            {
                searchRegion = new Mat(src, roi);
                disposeSearchRegion = true;
            }
        }

        try
        {
            using var preparedSearch = PrepareMatchImage(searchRegion, domain);
            using var preparedTemplate = PrepareMatchImage(template, domain);
            using var searchMask = PrepareSearchMask(inputs, roi, preparedSearch.Size());

            if (!HasSufficientSignal(preparedTemplate))
            {
                return Task.FromResult(CreateNoMatchOutput(src, GetMethodDescriptor(ResolveMatchMethod(method), domain), preparedTemplate.Size(), "Template contains insufficient texture for stable matching."));
            }

            if (preparedTemplate.Width > preparedSearch.Width || preparedTemplate.Height > preparedSearch.Height)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("模板尺寸不能大于搜索区域。"));
            }

            var matchMethod = ResolveMatchMethod(method);
            var isSqDiff = matchMethod == TemplateMatchModes.SqDiff || matchMethod == TemplateMatchModes.SqDiffNormed;

            using var result = new Mat();
            Cv2.MatchTemplate(preparedSearch, preparedTemplate, result, matchMethod);

            var matches = FindMatches(result, preparedTemplate.Size(), maxMatches, threshold, isSqDiff, searchMask);
            if (roi.X != 0 || roi.Y != 0)
            {
                matches = matches.Select(match => match.Offset(roi.X, roi.Y)).ToList();
            }

            var isMatch = matches.Count > 0;
            var resultImage = src.Clone();
            foreach (var match in matches)
            {
                Cv2.Rectangle(resultImage, match.TopLeft, match.BottomRight, new Scalar(0, 255, 0), 2);
                Cv2.DrawMarker(resultImage, new Point((int)Math.Round(match.Center.X), (int)Math.Round(match.Center.Y)), new Scalar(0, 0, 255), MarkerTypes.Cross, 20, 2);
                Cv2.PutText(
                    resultImage,
                    $"{match.Score:F3}",
                    new Point(match.TopLeft.X, Math.Max(16, match.TopLeft.Y - 8)),
                    HersheyFonts.HersheySimplex,
                    0.5,
                    new Scalar(0, 255, 0),
                    1);
            }

            var bestMatch = matches.FirstOrDefault();
            var position = bestMatch?.Center ?? new Position(0, 0);
            var methodDescriptor = GetMethodDescriptor(matchMethod, domain);
            if (!isMatch)
            {
                return Task.FromResult(CreateNoMatchOutput(src, methodDescriptor, preparedTemplate.Size(), "No match above threshold."));
            }

            var additionalData = new Dictionary<string, object>
            {
                ["IsMatch"] = true,
                ["Found"] = true,
                ["Score"] = bestMatch!.Score,
                ["Method"] = methodDescriptor,
                ["FailureReason"] = string.Empty,
                ["Position"] = position,
                ["X"] = position.X,
                ["Y"] = position.Y,
                ["MatchCount"] = matches.Count,
                ["Matches"] = matches.Select(m => new Dictionary<string, object>
                {
                    ["Position"] = m.Center,
                    ["TopLeft"] = new Position(m.TopLeft.X, m.TopLeft.Y),
                    ["Score"] = m.Score,
                    ["Width"] = preparedTemplate.Width,
                    ["Height"] = preparedTemplate.Height
                }).ToList(),
                ["TemplateWidth"] = preparedTemplate.Width,
                ["TemplateHeight"] = preparedTemplate.Height,
                ["Domain"] = NormalizeDomain(domain)
            };

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
        }
        finally
        {
            if (disposeSearchRegion)
            {
                searchRegion.Dispose();
            }
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 0.8);
        if (threshold < 0 || threshold > 1)
        {
            return ValidationResult.Invalid("阈值必须在 0-1 之间。");
        }

        var method = GetStringParam(@operator, "Method", "CCoeffNormed");
        var validMethods = new[] { "NCC", "SqDiff", "SqDiffNormed", "CCorr", "CCorrNormed", "CCoeff", "CCoeffNormed" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"不支持的匹配方法: {method}");
        }

        var maxMatches = GetIntParam(@operator, "MaxMatches", 1);
        if (maxMatches < 1 || maxMatches > 100)
        {
            return ValidationResult.Invalid("MaxMatches must be between 1 and 100.");
        }

        var domain = NormalizeDomain(GetStringParam(@operator, "Domain", "Gray"));
        if (domain is not ("Gray" or "Edge" or "Gradient"))
        {
            return ValidationResult.Invalid("Domain must be Gray, Edge, or Gradient.");
        }

        var roiWidth = GetIntParam(@operator, "RoiWidth", 0);
        var roiHeight = GetIntParam(@operator, "RoiHeight", 0);
        if (roiWidth < 0 || roiHeight < 0)
        {
            return ValidationResult.Invalid("ROI dimensions must be non-negative.");
        }

        return ValidationResult.Valid();
    }

    private static Mat PrepareMatchImage(Mat src, string domain)
    {
        using var gray = ToGray(src);
        return NormalizeDomain(domain) switch
        {
            "Edge" => BuildEdgeMap(gray),
            "Gradient" => BuildGradientMap(gray),
            _ => gray.Clone()
        };
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

    private static Mat BuildEdgeMap(Mat gray)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        using var otsuSource = new Mat();
        var otsuThreshold = Cv2.Threshold(blurred, otsuSource, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var low = Math.Max(10.0, otsuThreshold * 0.5);
        var high = Math.Max(low + 10.0, otsuThreshold);

        var edges = new Mat();
        Cv2.Canny(blurred, edges, low, high);
        return edges;
    }

    private static Mat BuildGradientMap(Mat gray)
    {
        using var gradX = new Mat();
        using var gradY = new Mat();
        using var magnitude = new Mat();
        Cv2.Sobel(gray, gradX, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_32F, 0, 1, 3);
        Cv2.Magnitude(gradX, gradY, magnitude);

        var normalized = new Mat();
        Cv2.Normalize(magnitude, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8UC1);
        return normalized;
    }

    private Mat? PrepareSearchMask(Dictionary<string, object>? inputs, Rect roi, Size searchSize)
    {
        if (!TryGetInputImage(inputs, "Mask", out var maskWrapper) || maskWrapper == null)
        {
            return null;
        }

        var sourceMask = maskWrapper.GetMat();
        if (sourceMask.Empty())
        {
            return null;
        }

        Mat maskRegion = sourceMask;
        var disposeMaskRegion = false;
        if (roi.X != 0 || roi.Y != 0 || roi.Width != sourceMask.Width || roi.Height != sourceMask.Height)
        {
            var clipped = roi.Intersect(new Rect(0, 0, sourceMask.Width, sourceMask.Height));
            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                return new Mat(searchSize, MatType.CV_8UC1, Scalar.Black);
            }

            maskRegion = new Mat(sourceMask, clipped);
            disposeMaskRegion = true;
        }

        try
        {
            using var grayMask = ToGray(maskRegion);
            var resized = new Mat();
            if (grayMask.Size() != searchSize)
            {
                Cv2.Resize(grayMask, resized, searchSize, 0, 0, InterpolationFlags.Nearest);
            }
            else
            {
                grayMask.CopyTo(resized);
            }

            Cv2.Threshold(resized, resized, 1, 255, ThresholdTypes.Binary);
            return resized;
        }
        finally
        {
            if (disposeMaskRegion)
            {
                maskRegion.Dispose();
            }
        }
    }

    private static TemplateMatchModes ResolveMatchMethod(string method)
    {
        return method.Trim().ToLowerInvariant() switch
        {
            "ncc" => TemplateMatchModes.CCoeffNormed,
            "sqdiff" => TemplateMatchModes.SqDiff,
            "sqdiffnormed" => TemplateMatchModes.SqDiffNormed,
            "ccorr" => TemplateMatchModes.CCorr,
            "ccorrnormed" => TemplateMatchModes.CCorrNormed,
            "ccoeff" => TemplateMatchModes.CCoeff,
            _ => TemplateMatchModes.CCoeffNormed
        };
    }

    private static List<TemplateMatchCandidate> FindMatches(
        Mat result,
        Size templateSize,
        int maxMatches,
        double threshold,
        bool isSqDiff,
        Mat? searchMask)
    {
        using var scoreMap = BuildScoreMap(result, isSqDiff);
        if (searchMask != null && !searchMask.Empty())
        {
            ApplySearchMask(scoreMap, searchMask, templateSize);
        }

        using var working = scoreMap.Clone();
        var candidates = new List<TemplateMatchCandidate>();
        var candidateBudget = Math.Max(maxMatches * 16, maxMatches);

        for (var index = 0; index < candidateBudget; index++)
        {
            Cv2.MinMaxLoc(working, out _, out var maxVal, out _, out var maxLoc);
            if (maxVal < threshold)
            {
                break;
            }

            candidates.Add(CreateCandidate(maxLoc, templateSize, maxVal));
            SuppressCandidateRegion(working, maxLoc, maxVal, threshold, templateSize);
        }

        return ApplyNms(candidates, 0.35)
            .Take(maxMatches)
            .ToList();
    }

    private static Mat BuildScoreMap(Mat result, bool isSqDiff)
    {
        var scoreMap = new Mat();
        if (isSqDiff)
        {
            Cv2.Subtract(Scalar.All(1.0), result, scoreMap);
            return scoreMap;
        }

        result.CopyTo(scoreMap);
        return scoreMap;
    }

    private static void ApplySearchMask(Mat scoreMap, Mat searchMask, Size templateSize)
    {
        var maskIndexer = searchMask.GetGenericIndexer<byte>();
        var width = searchMask.Width;
        var height = searchMask.Height;
        var integral = new int[height + 1, width + 1];

        for (var y = 0; y < height; y++)
        {
            var rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                rowSum += maskIndexer[y, x];
                integral[y + 1, x + 1] = integral[y, x + 1] + rowSum;
            }
        }

        var required = templateSize.Width * templateSize.Height * 255;
        for (var y = 0; y < scoreMap.Rows; y++)
        {
            for (var x = 0; x < scoreMap.Cols; x++)
            {
                var right = x + templateSize.Width;
                var bottom = y + templateSize.Height;
                var sum = integral[bottom, right] - integral[y, right] - integral[bottom, x] + integral[y, x];
                if (sum < required)
                {
                    scoreMap.Set(y, x, 0f);
                }
            }
        }
    }

    private static TemplateMatchCandidate CreateCandidate(Point topLeft, Size templateSize, double score)
    {
        var bounds = new Rect(topLeft.X, topLeft.Y, templateSize.Width, templateSize.Height);
        var center = new Position(topLeft.X + (templateSize.Width / 2.0), topLeft.Y + (templateSize.Height / 2.0));
        return new TemplateMatchCandidate(
            topLeft,
            new Point(bounds.Right, bounds.Bottom),
            center,
            score,
            bounds);
    }

    private static void SuppressCandidateRegion(Mat working, Point peakLocation, double peakScore, double threshold, Size templateSize)
    {
        var suppressionFloor = Math.Max(threshold, peakScore - Math.Max(0.02, (peakScore - threshold) * 0.25));
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

        using var suppressionRegion = new Mat(working, suppressBounds);
        suppressionRegion.SetTo(0f);
    }

    private static Rect ExpandRect(Rect rect, int paddingX, int paddingY, int maxWidth, int maxHeight)
    {
        var left = Math.Max(0, rect.X - paddingX);
        var top = Math.Max(0, rect.Y - paddingY);
        var right = Math.Min(maxWidth, rect.Right + paddingX);
        var bottom = Math.Min(maxHeight, rect.Bottom + paddingY);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static IEnumerable<TemplateMatchCandidate> ApplyNms(
        IEnumerable<TemplateMatchCandidate> candidates,
        double iouThreshold)
    {
        var selected = new List<TemplateMatchCandidate>();
        foreach (var candidate in candidates)
        {
            if (selected.All(existing => CalculateIoU(existing.Bounds, candidate.Bounds) < iouThreshold))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static double CalculateIoU(Rect a, Rect b)
    {
        var intersection = a & b;
        if (intersection.Width <= 0 || intersection.Height <= 0)
        {
            return 0;
        }

        var intersectionArea = intersection.Width * intersection.Height;
        var unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;
        return unionArea <= 0 ? 0 : (double)intersectionArea / unionArea;
    }

    private static string GetCanonicalMethodName(TemplateMatchModes method)
    {
        return method switch
        {
            TemplateMatchModes.SqDiff => "SqDiff",
            TemplateMatchModes.SqDiffNormed => "SqDiffNormed",
            TemplateMatchModes.CCorr => "CCorr",
            TemplateMatchModes.CCorrNormed => "CCorrNormed",
            TemplateMatchModes.CCoeff => "CCoeff",
            _ => "CCoeffNormed"
        };
    }

    private static string GetMethodDescriptor(TemplateMatchModes method, string domain)
    {
        var canonical = GetCanonicalMethodName(method);
        var normalizedDomain = NormalizeDomain(domain);
        return normalizedDomain == "Gray" ? canonical : $"{canonical}:{normalizedDomain}";
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().ToLowerInvariant() switch
        {
            "edge" => "Edge",
            "gradient" => "Gradient",
            _ => "Gray"
        };
    }

    private static bool HasSufficientSignal(Mat image)
    {
        if (image.Empty())
        {
            return false;
        }

        Cv2.MinMaxLoc(image, out var minValue, out var maxValue, out Point _, out Point _);
        return (maxValue - minValue) >= 1.0;
    }

    private OperatorExecutionOutput CreateNoMatchOutput(Mat sourceImage, string methodDescriptor, Size templateSize, string failureReason)
    {
        var resultImage = sourceImage.Clone();
        var position = new Position(0, 0);
        var output = new Dictionary<string, object>
        {
            ["IsMatch"] = false,
            ["Found"] = false,
            ["Score"] = 0.0,
            ["Method"] = methodDescriptor,
            ["FailureReason"] = failureReason,
            ["Position"] = position,
            ["X"] = position.X,
            ["Y"] = position.Y,
            ["MatchCount"] = 0,
            ["Matches"] = Array.Empty<object>(),
            ["TemplateWidth"] = templateSize.Width,
            ["TemplateHeight"] = templateSize.Height,
            ["Message"] = failureReason
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    private sealed record TemplateMatchCandidate(Point TopLeft, Point BottomRight, Position Center, double Score, Rect Bounds)
    {
        public TemplateMatchCandidate Offset(int offsetX, int offsetY)
        {
            var offsetTopLeft = new Point(TopLeft.X + offsetX, TopLeft.Y + offsetY);
            var offsetBottomRight = new Point(BottomRight.X + offsetX, BottomRight.Y + offsetY);
            var offsetCenter = new Position(Center.X + offsetX, Center.Y + offsetY);
            var offsetBounds = new Rect(Bounds.X + offsetX, Bounds.Y + offsetY, Bounds.Width, Bounds.Height);
            return new TemplateMatchCandidate(offsetTopLeft, offsetBottomRight, offsetCenter, Score, offsetBounds);
        }
    }
}

