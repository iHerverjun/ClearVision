// TemplateMatchOperator.cs
// 模板匹配算子 - 在图像中查找模板位置
// 作者：蘅芜君

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
    Description = "基于 OpenCV MatchTemplate 的模板匹配，支持单目标与多目标输出。",
    Category = "匹配定位",
    IconName = "template",
    Keywords = new[] { "模板匹配", "定位", "找图", "Template", "Match", "Locate" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = true)]
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
[OperatorParam("Threshold", "匹配分数阈值", "double", DefaultValue = 0.8, Min = 0.0, Max = 1.0)]
[OperatorParam("MaxMatches", "最大匹配数", "int", DefaultValue = 1, Min = 1, Max = 100)]
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
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        if (!TryGetInputImage(inputs, "Template", out var templateWrapper) || templateWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像"));
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 0.8, min: 0, max: 1);
        var method = GetStringParam(@operator, "Method", "CCoeffNormed");
        var maxMatches = GetIntParam(@operator, "MaxMatches", 1, min: 1, max: 100);

        var src = imageWrapper.GetMat();
        var template = templateWrapper.GetMat();

        if (src.Empty() || template.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码图像"));
        }

        if (template.Width > src.Width || template.Height > src.Height)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("模板尺寸不能大于源图像"));
        }

        using var srcGray = ToGray(src);
        using var templateGray = ToGray(template);

        var matchMethod = ResolveMatchMethod(method);
        var isSqDiff = matchMethod == TemplateMatchModes.SqDiff || matchMethod == TemplateMatchModes.SqDiffNormed;

        using var result = new Mat();
        Cv2.MatchTemplate(srcGray, templateGray, result, matchMethod);

        var matches = FindMatches(result, templateGray.Size(), maxMatches, threshold, isSqDiff);
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
        var additionalData = new Dictionary<string, object>
        {
            ["IsMatch"] = isMatch,
            ["Found"] = isMatch,
            ["Score"] = bestMatch?.Score ?? 0.0,
            ["Position"] = position,
            ["X"] = position.X,
            ["Y"] = position.Y,
            ["MatchCount"] = matches.Count,
            ["Matches"] = matches.Select(m => new Dictionary<string, object>
            {
                ["Position"] = m.Center,
                ["TopLeft"] = new Position(m.TopLeft.X, m.TopLeft.Y),
                ["Score"] = m.Score,
                ["Width"] = templateGray.Width,
                ["Height"] = templateGray.Height
            }).ToList(),
            ["TemplateWidth"] = templateGray.Width,
            ["TemplateHeight"] = templateGray.Height
        };

        if (!isMatch)
        {
            additionalData["Message"] = "No match above threshold.";
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 0.8);
        if (threshold < 0 || threshold > 1)
        {
            return ValidationResult.Invalid("阈值必须在 0-1 之间");
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
        bool isSqDiff)
    {
        var working = result.Clone();
        try
        {
            var matches = new List<TemplateMatchCandidate>();
            for (var index = 0; index < maxMatches; index++)
            {
                Cv2.MinMaxLoc(working, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                var score = isSqDiff ? 1.0 - minVal : maxVal;
                var topLeft = isSqDiff ? minLoc : maxLoc;
                if (score < threshold)
                {
                    break;
                }

                var center = new Position(topLeft.X + (templateSize.Width / 2.0), topLeft.Y + (templateSize.Height / 2.0));
                matches.Add(new TemplateMatchCandidate(
                    topLeft,
                    new Point(topLeft.X + templateSize.Width, topLeft.Y + templateSize.Height),
                    center,
                    score));

                var suppressX = Math.Max(0, topLeft.X - (templateSize.Width / 4));
                var suppressY = Math.Max(0, topLeft.Y - (templateSize.Height / 4));
                var suppressRight = Math.Min(working.Width, topLeft.X + templateSize.Width + (templateSize.Width / 4));
                var suppressBottom = Math.Min(working.Height, topLeft.Y + templateSize.Height + (templateSize.Height / 4));
                if (suppressRight <= suppressX || suppressBottom <= suppressY)
                {
                    continue;
                }

                using var roi = new Mat(working, new Rect(suppressX, suppressY, suppressRight - suppressX, suppressBottom - suppressY));
                roi.SetTo(isSqDiff ? Scalar.All(1.0) : Scalar.All(0.0));
            }

            return matches;
        }
        finally
        {
            working.Dispose();
        }
    }

    private sealed record TemplateMatchCandidate(Point TopLeft, Point BottomRight, Position Center, double Score);
}
