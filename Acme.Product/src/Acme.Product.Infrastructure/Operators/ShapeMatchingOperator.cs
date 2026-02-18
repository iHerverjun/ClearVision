// ShapeMatchingOperator.cs
// 形状匹配算子 - 旋转// 功能实现缩放不变模板匹配
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Collections.Concurrent;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 形状匹配算子 - 旋转/缩放不变模板匹配
/// </summary>
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
        // 1. 获取搜索图像
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 2. 获取参数
        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var minScore = GetDoubleParam(@operator, "MinScore", 0.7, min: 0.1, max: 1.0);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 1, min: 1, max: 50);
        var angleStart = GetDoubleParam(@operator, "AngleStart", -30.0, min: -180.0, max: 180.0);
        var angleExtent = GetDoubleParam(@operator, "AngleExtent", 60.0, min: 0.0, max: 360.0);
        var angleStep = GetDoubleParam(@operator, "AngleStep", 1.0, min: 0.1, max: 10.0);
        var numLevels = GetIntParam(@operator, "NumLevels", 3, min: 1, max: 6);

        // 3. 获取搜索图像 Mat
        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 4. 获取模板图像
        Mat? templateMat = null;
        bool shouldDisposeTemplate = false;

        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateMat = templateWrapper.GetMat();
            shouldDisposeTemplate = false;
        }
        else if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
        {
            templateMat = Cv2.ImRead(templatePath, ImreadModes.Color);
            shouldDisposeTemplate = true;
        }

        if (templateMat == null || templateMat.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像，请连接模板输入或设置模板文件路径"));
        }

        try
        {
            // 创建结果图像
            using var resultImage = src.Clone();

            // 转换为灰度
            using var srcGray = new Mat();
            using var tmplGray = new Mat();
            Cv2.CvtColor(src, srcGray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(templateMat, tmplGray, ColorConversionCodes.BGR2GRAY);

            // 构建旋转模板集
            var angles = Enumerable.Range(0, (int)((angleExtent) / angleStep) + 1)
                .Select(i => angleStart + i * angleStep)
                .Where(a => a <= angleStart + angleExtent)
                .ToList();

            var matches = new ConcurrentBag<MatchResult>();

            // 并行处理每个角度
            Parallel.ForEach(angles, angle =>
            {
                try
                {
                    // 旋转模板
                    using var rotatedTmpl = RotateImage(tmplGray, angle);
                    if (rotatedTmpl.Width > srcGray.Width || rotatedTmpl.Height > srcGray.Height)
                        return;

                    // 模板匹配
                    using var matchResult = new Mat();
                    Cv2.MatchTemplate(srcGray, rotatedTmpl, matchResult, TemplateMatchModes.CCoeffNormed);

                    // 查找局部最大值
                    Cv2.MinMaxLoc(matchResult, out _, out double maxVal, out _, out Point maxLoc);

                    if (maxVal >= minScore)
                    {
                        matches.Add(new MatchResult
                        {
                            X = maxLoc.X,
                            Y = maxLoc.Y,
                            Angle = angle,
                            Score = maxVal,
                            Width = rotatedTmpl.Width,
                            Height = rotatedTmpl.Height
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "角度 {Angle} 匹配失败: {Message}", angle, ex.Message);
                }
            });

            // NMS 去除重叠匹配
            var filteredMatches = NonMaximumSuppression(matches.ToList(), 0.5f);
            var finalMatches = filteredMatches.Take(maxMatches).ToList();

            // 绘制匹配结果
            foreach (var match in finalMatches)
            {
                DrawMatchResult(resultImage, match, tmplGray);
            }

            // 构建输出数据
            var matchResults = finalMatches.Select(m => new Dictionary<string, object>
            {
                { "X", m.X },
                { "Y", m.Y },
                { "Angle", m.Angle },
                { "Score", m.Score },
                { "CenterX", m.X + m.Width / 2.0 },
                { "CenterY", m.Y + m.Height / 2.0 }
            }).ToList();

            var additionalData = new Dictionary<string, object>
            {
                { "Matches", matchResults },
                { "MatchCount", matchResults.Count }
            };

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
        }
        finally
        {
            if (shouldDisposeTemplate && templateMat != null)
            {
                templateMat.Dispose();
            }
        }
    }

    private Mat RotateImage(Mat src, double angle)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        using var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var rotated = new Mat();
        Cv2.WarpAffine(src, rotated, rotMatrix, src.Size());
        return rotated;
    }

    private List<MatchResult> NonMaximumSuppression(List<MatchResult> matches, float iouThreshold)
    {
        var sorted = matches.OrderByDescending(m => m.Score).ToList();
        var result = new List<MatchResult>();

        while (sorted.Any())
        {
            var best = sorted.First();
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = sorted.Where(m => CalculateIoU(best, m) < iouThreshold).ToList();
        }

        return result;
    }

    private float CalculateIoU(MatchResult a, MatchResult b)
    {
        var rectA = new Rect(a.X, a.Y, a.Width, a.Height);
        var rectB = new Rect(b.X, b.Y, b.Width, b.Height);

        var intersection = Rect.Intersect(rectA, rectB);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0f;

        var interArea = intersection.Width * intersection.Height;
        var unionArea = rectA.Width * rectA.Height + rectB.Width * rectB.Height - interArea;

        return (float)interArea / unionArea;
    }

    private void DrawMatchResult(Mat image, MatchResult match, Mat template)
    {
        // 绘制匹配矩形
        var rect = new Rect(match.X, match.Y, match.Width, match.Height);
        Cv2.Rectangle(image, rect, new Scalar(0, 255, 0), 2);

        // 绘制中心点
        var center = new Point((int)(match.X + match.Width / 2.0), (int)(match.Y + match.Height / 2.0));
        Cv2.Circle(image, center, 5, new Scalar(0, 0, 255), -1);

        // 显示分数和角度
        var text = $"{match.Score:F2} @{match.Angle:F0}";
        Cv2.PutText(image, text, new Point(match.X, match.Y - 5),
            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 0, 0), 1);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetDoubleParam(@operator, "MinScore", 0.7);
        if (minScore < 0.1 || minScore > 1.0)
            return ValidationResult.Invalid("最小匹配分数必须在 0.1-1.0 之间");

        var maxMatches = GetIntParam(@operator, "MaxMatches", 1);
        if (maxMatches < 1 || maxMatches > 50)
            return ValidationResult.Invalid("最大匹配数必须在 1-50 之间");

        var angleStep = GetDoubleParam(@operator, "AngleStep", 1.0);
        if (angleStep < 0.1 || angleStep > 10.0)
            return ValidationResult.Invalid("角度步长必须在 0.1-10.0 之间");

        var numLevels = GetIntParam(@operator, "NumLevels", 3);
        if (numLevels < 1 || numLevels > 6)
            return ValidationResult.Invalid("金字塔层数必须在 1-6 之间");

        return ValidationResult.Valid();
    }

    private class MatchResult
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Angle { get; set; }
        public double Score { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
