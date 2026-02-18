// PyramidShapeMatchOperator.cs
// 金字塔形状匹配算子
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 金字塔形状匹配算子
/// </summary>
public class PyramidShapeMatchOperator : OperatorBase
{
    private readonly Dictionary<string, object> _matcherCache = new();
    public override OperatorType OperatorType => OperatorType.PyramidShapeMatch;

    public PyramidShapeMatchOperator(ILogger<PyramidShapeMatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var minScore = GetDoubleParam(@operator, "MinScore", 80.0, min: 0.0, max: 100.0);
        var angleRange = GetIntParam(@operator, "AngleRange", 180, min: 0, max: 180);
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, min: 1, max: 5);
        var magnitudeThreshold = GetIntParam(@operator, "MagnitudeThreshold", 30, min: 0, max: 255);

        Mat? templateFromInput = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateFromInput = templateWrapper.GetMat();
        }

        using var srcImage = imageWrapper.GetMat();
        
        try
        {
            // 使用CPU密集型计算
            return RunCpuBoundWork(() =>
            {
                // TODO: 实现PyramidShapeMatcher
                // 目前使用GradientShapeMatcher作为简化实现
                var matcher = new GradientShapeMatcher(magnitudeThreshold, 1);
                
                // 训练模板
                if (templateFromInput != null)
                {
                    matcher.Train(templateFromInput, angleRange);
                }
                else if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                {
                    using var templateImg = Cv2.ImRead(templatePath, ImreadModes.Color);
                    matcher.Train(templateImg, angleRange);
                }
                else
                {
                    return OperatorExecutionOutput.Failure("未提供模板图像或路径");
                }

                // 执行匹配
                var result = matcher.Match(srcImage, minScore);

                // 创建输出图像
                using var resultImage = srcImage.Clone();
                var boxColor = result.IsValid ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

                if (result.IsValid)
                {
                    int size = 40;
                    Cv2.Rectangle(resultImage, 
                        new Point(result.Position.X - size, result.Position.Y - size),
                        new Point(result.Position.X + size, result.Position.Y + size),
                        boxColor, 2);
                    Cv2.DrawMarker(resultImage, result.Position, boxColor, MarkerTypes.Cross, 20, 2);
                }

                string info = $"{(result.IsValid ? "OK" : "NG")}: Score={result.Score:F1}%";
                Cv2.PutText(resultImage, info, new Point(10, 30), 
                    HersheyFonts.HersheySimplex, 0.6, boxColor, 2);

                if (result.IsValid)
                {
                    string angleInfo = $"Angle: {result.Angle:F1}°";
                    Cv2.PutText(resultImage, angleInfo, new Point(result.Position.X - 40, result.Position.Y - 45), 
                        HersheyFonts.HersheySimplex, 0.5, boxColor, 1);
                }

                matcher.Dispose();

                return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
                {
                    { "IsMatch", result.IsValid },
                    { "Score", result.Score },
                    { "X", result.Position.X },
                    { "Y", result.Position.Y },
                    { "Angle", result.Angle },
                    { "PyramidLevels", pyramidLevels }
                }));
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"金字塔形状匹配失败: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetDoubleParam(@operator, "MinScore", 80.0);
        if (minScore < 0 || minScore > 100)
        {
            return ValidationResult.Invalid("最小分数必须在 0-100 之间");
        }

        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3);
        if (pyramidLevels < 1 || pyramidLevels > 5)
        {
            return ValidationResult.Invalid("金字塔层数必须在 1-5 之间");
        }

        return ValidationResult.Valid();
    }
}
