using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "金字塔形状匹配",
    Description = "基于 LINEMOD 的金字塔模板匹配。",
    Category = "匹配定位",
    IconName = "pyramid-match"
)]
[InputPort("Image", "搜索图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Position", "匹配位置", PortDataType.Point)]
[OutputPort("Angle", "旋转角度", PortDataType.Float)]
[OutputPort("IsMatch", "是否匹配", PortDataType.Boolean)]
[OutputPort("Score", "匹配分数", PortDataType.Float)]
[OperatorParam("TemplatePath", "模板路径", "file", DefaultValue = "")]
[OperatorParam("MinScore", "最小分数(%)", "double", DefaultValue = 80.0, Min = 0.0, Max = 100.0)]
[OperatorParam("AngleRange", "角度范围(度)", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("AngleStep", "角度步长", "int", DefaultValue = 5, Min = 1, Max = 45)]
[OperatorParam("PyramidLevels", "金字塔层数", "int", DefaultValue = 3, Min = 1, Max = 5)]
[OperatorParam("MagnitudeThreshold", "梯度阈值", "int", DefaultValue = 30, Min = 0, Max = 255)]
[OperatorParam("WeakThreshold", "弱梯度阈值", "double", DefaultValue = 30.0, Min = 0.0, Max = 255.0)]
[OperatorParam("StrongThreshold", "强梯度阈值", "double", DefaultValue = 60.0, Min = 0.0, Max = 255.0)]
[OperatorParam("NumFeatures", "特征点数量", "int", DefaultValue = 150, Min = 50, Max = 8191)]
[OperatorParam("SpreadT", "方向扩展范围", "int", DefaultValue = 4, Min = 1, Max = 16)]
[OperatorParam("MaxMatches", "最大匹配数", "int", DefaultValue = 10, Min = 1, Max = 100)]
[OperatorParam("MatchMode", "匹配模式", "enum", DefaultValue = "Template", Options = new[] { "Template|模板匹配", "ShapeDescriptor|形状描述符匹配" })]
[OperatorParam("DescriptorTypes", "描述符类型", "enum", DefaultValue = "Hu+Fourier", Options = new[] { "Hu|Hu矩", "Fourier|傅里叶描述符", "Hu+Fourier|全部" })]
[OperatorParam("PreFilterArea", "面积预筛选", "bool", DefaultValue = true)]
[OperatorParam("AreaTolerance", "面积容差", "double", DefaultValue = 0.3, Min = 0.0, Max = 1.0)]
public class PyramidShapeMatchOperator : OperatorBase
{
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
        var minScore = GetFloatParam(@operator, "MinScore", 80.0f, min: 0.0f, max: 100.0f);
        var matchMode = GetStringParam(@operator, "MatchMode", "Template");
        var angleRange = GetIntParam(@operator, "AngleRange", 0, min: 0, max: 180);
        var angleStep = GetIntParam(@operator, "AngleStep", 5, min: 1, max: 45);
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, min: 1, max: 5);
        var magnitudeThreshold = GetIntParam(@operator, "MagnitudeThreshold", 30, min: 0, max: 255);
        var weakThreshold = GetFloatParam(@operator, "WeakThreshold", 30.0f, min: 0.0f, max: 255.0f);
        var strongThreshold = GetFloatParam(@operator, "StrongThreshold", 60.0f, min: 0.0f, max: 255.0f);
        var numFeatures = GetIntParam(@operator, "NumFeatures", 150, min: 50, max: 8191);
        var spreadT = GetIntParam(@operator, "SpreadT", 4, min: 1, max: 16);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 10, min: 1, max: 100);
        var descriptorTypes = GetStringParam(@operator, "DescriptorTypes", "Hu+Fourier");
        var preFilterArea = GetBoolParam(@operator, "PreFilterArea", true);
        var areaTolerance = GetDoubleParam(@operator, "AreaTolerance", 0.3, 0.0, 1.0);

        Mat? templateFromInput = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateFromInput = templateWrapper.GetMat().Clone();
        }

        var srcImage = imageWrapper.GetMat();

        try
        {
            return RunCpuBoundWork(() =>
            {
                IShapeMatcher? matcher = null;
                try
                {
                    if (matchMode.Equals("ShapeDescriptor", StringComparison.OrdinalIgnoreCase))
                    {
                        matcher = new ShapeDescriptorMatcher(new ShapeDescriptorConfig
                        {
                            UseHuMoments = descriptorTypes.Contains("Hu", StringComparison.OrdinalIgnoreCase),
                            UseFourierDescriptors = descriptorTypes.Contains("Fourier", StringComparison.OrdinalIgnoreCase),
                            AreaTolerance = areaTolerance
                        });
                    }
                    else
                    {
                        matcher = new TemplateMatcher(new TemplateMatcherConfig
                        {
                            PyramidLevels = pyramidLevels,
                            AngleRange = angleRange,
                            AngleStep = angleStep,
                            WeakThreshold = weakThreshold,
                            StrongThreshold = strongThreshold,
                            NumFeatures = numFeatures,
                            SpreadT = spreadT,
                            MagnitudeThreshold = magnitudeThreshold
                        });
                    }

                    bool trained;
                    if (templateFromInput != null)
                    {
                        trained = matcher.Train(templateFromInput);
                    }
                    else if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                    {
                        using var templateImg = Cv2.ImRead(templatePath, ImreadModes.Color);
                        if (templateImg.Empty())
                        {
                            return OperatorExecutionOutput.Failure($"无法加载模板图像: {templatePath}");
                        }

                        trained = matcher.Train(templateImg);
                    }
                    else
                    {
                        return OperatorExecutionOutput.Failure("未提供模板图像或路径");
                    }

                    if (!trained)
                    {
                        return OperatorExecutionOutput.Failure("模板训练失败");
                    }

                    var matches = matcher.Match(srcImage, minScore, maxMatches);
                    var resultImage = srcImage.Clone();
                    var (primaryMatch, allMatches) = DrawShapeMatchResults(resultImage, matches, minScore);
                    var matcherConfig = matcher.GetConfig();

                    var outputData = new Dictionary<string, object>
                    {
                        ["IsMatch"] = primaryMatch.IsValid,
                        ["Score"] = primaryMatch.Score,
                        ["Position"] = primaryMatch.Position,
                        ["X"] = primaryMatch.Position.X,
                        ["Y"] = primaryMatch.Position.Y,
                        ["Angle"] = primaryMatch.Angle,
                        ["MatchCount"] = allMatches.Count,
                        ["MatchMode"] = matchMode,
                        ["ScoreScale"] = "Percent",
                        ["MatcherConfig"] = matcherConfig,
                        ["MatcherDiagnostics"] = BuildMatcherDiagnostics(
                            matchMode,
                            magnitudeThreshold,
                            weakThreshold,
                            strongThreshold,
                            numFeatures,
                            spreadT,
                            preFilterArea,
                            areaTolerance)
                    };

                    var matchList = new List<Dictionary<string, object>>();
                    for (var i = 0; i < allMatches.Count && i < maxMatches; i++)
                    {
                        var match = allMatches[i];
                        matchList.Add(new Dictionary<string, object>
                        {
                            ["Position"] = match.Position,
                            ["X"] = match.Position.X,
                            ["Y"] = match.Position.Y,
                            ["Score"] = match.Score,
                            ["Angle"] = match.Angle,
                            ["Scale"] = match.Scale,
                            ["Metadata"] = new Dictionary<string, object>(match.Metadata)
                            {
                                ["ScoreScale"] = "Percent"
                            }
                        });

                        outputData[$"Match{i}_X"] = match.Position.X;
                        outputData[$"Match{i}_Y"] = match.Position.Y;
                        outputData[$"Match{i}_Score"] = match.Score;
                        outputData[$"Match{i}_Angle"] = match.Angle;
                    }

                    outputData["Matches"] = matchList;
                    return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData));
                }
                finally
                {
                    matcher?.Dispose();
                    templateFromInput?.Dispose();
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            templateFromInput?.Dispose();
            return Task.FromResult(OperatorExecutionOutput.Failure($"金字塔形状匹配失败: {ex.Message}"));
        }
    }

    private static Dictionary<string, object> BuildMatcherDiagnostics(
        string matchMode,
        int magnitudeThreshold,
        float weakThreshold,
        float strongThreshold,
        int numFeatures,
        int spreadT,
        bool preFilterArea,
        double areaTolerance)
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["Mode"] = matchMode,
            ["AppliedParameters"] = new Dictionary<string, object>
            {
                ["WeakThreshold"] = weakThreshold,
                ["StrongThreshold"] = strongThreshold,
                ["NumFeatures"] = numFeatures,
                ["SpreadT"] = spreadT
            }
        };

        diagnostics["LegacyParameters"] = matchMode.Equals("Template", StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, object>
            {
                ["MagnitudeThreshold"] = magnitudeThreshold,
                ["Reason"] = "LINEMOD 路径当前以 WeakThreshold/StrongThreshold 为主，MagnitudeThreshold 仅保留兼容诊断。"
            }
            : new Dictionary<string, object>
            {
                ["MagnitudeThreshold"] = magnitudeThreshold,
                ["WeakThreshold"] = weakThreshold,
                ["StrongThreshold"] = strongThreshold,
                ["NumFeatures"] = numFeatures,
                ["SpreadT"] = spreadT,
                ["Reason"] = "ShapeDescriptor 模式下这些 LINEMOD 参数不参与实际匹配。"
            };

        diagnostics["ShapeDescriptorOnly"] = new Dictionary<string, object>
        {
            ["PreFilterArea"] = preFilterArea,
            ["AreaTolerance"] = areaTolerance
        };

        return diagnostics;
    }

    private static (Acme.Product.Infrastructure.ImageProcessing.ShapeMatchResult primary, List<Acme.Product.Infrastructure.ImageProcessing.ShapeMatchResult> all) DrawShapeMatchResults(
        Mat image,
        List<Acme.Product.Infrastructure.ImageProcessing.ShapeMatchResult> matches,
        float minScore)
    {
        var validMatches = matches.Where(m => m.IsValid).ToList();
        var primaryMatch = validMatches.FirstOrDefault() ?? new Acme.Product.Infrastructure.ImageProcessing.ShapeMatchResult { IsValid = false };

        for (var i = 0; i < validMatches.Count; i++)
        {
            var match = validMatches[i];
            var color = i == 0 ? new Scalar(0, 255, 0) : new Scalar(255, 255, 0);
            var halfW = Math.Max(1, match.TemplateWidth / 2);
            var halfH = Math.Max(1, match.TemplateHeight / 2);
            var topLeft = new Point(match.Position.X - halfW, match.Position.Y - halfH);
            var bottomRight = new Point(match.Position.X + halfW, match.Position.Y + halfH);

            Cv2.Rectangle(image, topLeft, bottomRight, color, 2);
            Cv2.DrawMarker(image, match.Position, color, MarkerTypes.Cross, 20, 2);
            Cv2.PutText(image, $"{match.Score:F1}%", new Point(topLeft.X, topLeft.Y - 5), HersheyFonts.HersheySimplex, 0.5, color, 1);

            if (Math.Abs(match.Angle) > 0.1f)
            {
                Cv2.PutText(image, $"{match.Angle:F1}deg", new Point(topLeft.X, topLeft.Y + 15), HersheyFonts.HersheySimplex, 0.4, color, 1);
            }
        }

        var statusText = primaryMatch.IsValid
            ? $"OK: found {validMatches.Count} match(es)"
            : $"NG: no match >= {minScore:F1}%";
        var statusColor = primaryMatch.IsValid ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);
        Cv2.PutText(image, statusText, new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, statusColor, 2);

        if (primaryMatch.IsValid)
        {
            Cv2.PutText(
                image,
                $"Best ({primaryMatch.Position.X}, {primaryMatch.Position.Y}) score={primaryMatch.Score:F1}%",
                new Point(10, 55),
                HersheyFonts.HersheySimplex,
                0.5,
                statusColor,
                1);
        }

        return (primaryMatch, validMatches);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetFloatParam(@operator, "MinScore", 80.0f);
        if (minScore < 0 || minScore > 100)
        {
            return ValidationResult.Invalid("最小分数必须在 0-100 之间");
        }

        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3);
        if (pyramidLevels < 1 || pyramidLevels > 5)
        {
            return ValidationResult.Invalid("金字塔层数必须在 1-5 之间");
        }

        var numFeatures = GetIntParam(@operator, "NumFeatures", 150);
        if (numFeatures < 50 || numFeatures > 8191)
        {
            return ValidationResult.Invalid("特征点数量必须在 50-8191 之间");
        }

        var spreadT = GetIntParam(@operator, "SpreadT", 4);
        if (spreadT < 1 || spreadT > 16)
        {
            return ValidationResult.Invalid("SpreadT 必须在 1-16 之间");
        }

        var angleRange = GetIntParam(@operator, "AngleRange", 0);
        if (angleRange < 0 || angleRange > 180)
        {
            return ValidationResult.Invalid("角度范围必须在 0-180 之间");
        }

        var angleStep = GetIntParam(@operator, "AngleStep", 5);
        if (angleStep < 1 || angleStep > 45)
        {
            return ValidationResult.Invalid("角度步长必须在 1-45 之间");
        }

        return ValidationResult.Valid();
    }
}
