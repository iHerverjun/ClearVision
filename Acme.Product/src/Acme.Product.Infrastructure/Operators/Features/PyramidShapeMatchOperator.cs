// PyramidShapeMatchOperator.cs
// 金字塔形状匹配算子 - 基于 LINEMOD 算法实现
// 参考实现: meiqua/shape_based_matching
// 论文: Hinterstoisser et al., "Gradient Response Maps for Real-Time Detection of Texture-Less Objects", IEEE TPAMI 2012
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using ShapeMatchResult = Acme.Product.Infrastructure.ImageProcessing.ShapeMatchResult;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 金字塔形状匹配算子 - 基于 LINEMOD 算法
/// 
/// 核心特性:
/// 1. Hysteresis 方向量化 + 3x3 邻域投票稳定性检查
/// 2. NMS 非极大值抑制特征提取
/// 3. 稀疏特征选择 (距离约束确保空间均匀分布)
/// 4. 方向扩展 (Spreading) 增加鲁棒性
/// 5. 响应图 (Response Maps) + LUT 查表加速
/// 6. 金字塔粗到精搜索 (Coarse-to-Fine)
/// 7. 多目标检测与 NMS 去重
/// </summary>
[OperatorMeta(
    DisplayName = "金字塔形状匹配",
    Description = "多尺度金字塔形状匹配，速度快，适合大尺寸图像",
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
[OperatorParam("AngleRange", "角度范围(±)", "int", DefaultValue = 180, Min = 0, Max = 180)]
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
    private readonly Dictionary<string, List<Template>> _templateCache = new();
    private readonly object _cacheLock = new();

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

        // 获取参数
        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var minScore = GetFloatParam(@operator, "MinScore", 80.0f, min: 0.0f, max: 100.0f);
        var matchMode = GetStringParam(@operator, "MatchMode", "Template");
        var angleRange = GetIntParam(@operator, "AngleRange", 0, min: 0, max: 180);
        var angleStep = GetIntParam(@operator, "AngleStep", 5, min: 1, max: 45);
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, min: 1, max: 5);
        var weakThreshold = GetFloatParam(@operator, "WeakThreshold", 30.0f, min: 0.0f, max: 255.0f);
        var strongThreshold = GetFloatParam(@operator, "StrongThreshold", 60.0f, min: 0.0f, max: 255.0f);
        var numFeatures = GetIntParam(@operator, "NumFeatures", 150, min: 50, max: 8191);
        var spreadT = GetIntParam(@operator, "SpreadT", 4, min: 1, max: 16);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 10, min: 1, max: 100);
        var descriptorTypes = GetStringParam(@operator, "DescriptorTypes", "Hu+Fourier");
        var preFilterArea = GetBoolParam(@operator, "PreFilterArea", true);
        var areaTolerance = GetDoubleParam(@operator, "AreaTolerance", 0.3, 0.0, 1.0);

        // 获取模板图像 (优先从输入获取)
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
                // 根据匹配模式创建对应的匹配器
                IShapeMatcher matcher;
                
                if (matchMode.Equals("ShapeDescriptor", StringComparison.OrdinalIgnoreCase))
                {
                    // 形状描述符匹配器
                    var config = new ShapeDescriptorConfig
                    {
                        UseHuMoments = descriptorTypes.Contains("Hu"),
                        UseFourierDescriptors = descriptorTypes.Contains("Fourier"),
                        AreaTolerance = areaTolerance
                    };
                    matcher = new ShapeDescriptorMatcher(config);
                }
                else
                {
                    // 模板匹配器（LINEMOD）
                    matcher = new TemplateMatcher(pyramidLevels, angleRange, angleStep);
                }

                // 训练模板
                bool trained = false;
                if (templateFromInput != null)
                {
                    trained = matcher.Train(templateFromInput);
                }
                else if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
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

                // 执行匹配
                var matches = matcher.Match(srcImage, minScore, maxMatches);

                // 创建结果图像
                var resultImage = srcImage.Clone();

                // 绘制匹配结果
                var (primaryMatch, allMatches) = DrawShapeMatchResults(resultImage, matches, minScore);

                // 构建统一格式的输出数据
                var outputData = new Dictionary<string, object>
                {
                    { "IsMatch", primaryMatch.IsValid },
                    { "Score", primaryMatch.Score },
                    { "Position", primaryMatch.Position },
                    { "X", primaryMatch.Position.X },
                    { "Y", primaryMatch.Position.Y },
                    { "Angle", primaryMatch.Angle },
                    { "MatchCount", allMatches.Count },
                    { "MatchMode", matchMode }
                };

                // 添加所有匹配结果（统一格式）
                var matchesList = new List<Dictionary<string, object>>();
                for (int i = 0; i < allMatches.Count && i < maxMatches; i++)
                {
                    var match = allMatches[i];
                    matchesList.Add(new Dictionary<string, object>
                    {
                        { "Position", match.Position },
                        { "X", match.Position.X },
                        { "Y", match.Position.Y },
                        { "Score", match.Score },
                        { "Angle", match.Angle },
                        { "Scale", match.Scale }
                    });
                    
                    // 保留旧格式的散落键（向后兼容）
                    outputData[$"Match{i}_X"] = match.Position.X;
                    outputData[$"Match{i}_Y"] = match.Position.Y;
                    outputData[$"Match{i}_Score"] = match.Score;
                    outputData[$"Match{i}_Angle"] = match.Angle;
                }
                outputData["Matches"] = matchesList;

                // 添加匹配器配置信息
                var configInfo = matcher.GetConfig();
                outputData["MatcherConfig"] = configInfo;

                // 释放资源
                matcher.Dispose();
                templateFromInput?.Dispose();

                return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData));
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            templateFromInput?.Dispose();
            return Task.FromResult(OperatorExecutionOutput.Failure($"金字塔形状匹配失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 绘制形状匹配结果（统一接口）
    /// </summary>
    private (ImageProcessing.ShapeMatchResult primary, List<ImageProcessing.ShapeMatchResult> all) DrawShapeMatchResults(
        Mat image,
        List<ImageProcessing.ShapeMatchResult> matches,
        float minScore)
    {
        var validMatches = matches.Where(m => m.IsValid).ToList();
        var primaryMatch = validMatches.FirstOrDefault() ?? new ImageProcessing.ShapeMatchResult { IsValid = false };

        // 绘制所有匹配结果
        for (int i = 0; i < validMatches.Count; i++)
        {
            var match = validMatches[i];
            var color = i == 0
                ? new Scalar(0, 255, 0)    // 最佳匹配 - 绿色
                : new Scalar(255, 255, 0); // 其他匹配 - 青色

            // 计算矩形框
            int halfW = match.TemplateWidth / 2;
            int halfH = match.TemplateHeight / 2;

            var topLeft = new Point(match.Position.X - halfW, match.Position.Y - halfH);
            var bottomRight = new Point(match.Position.X + halfW, match.Position.Y + halfH);

            // 绘制矩形框
            Cv2.Rectangle(image, topLeft, bottomRight, color, 2);

            // 绘制中心点
            Cv2.DrawMarker(image, match.Position, color, MarkerTypes.Cross, 20, 2);

            // 绘制得分和角度
            string scoreText = $"{match.Score:F1}%";
            Cv2.PutText(image, scoreText, new Point(topLeft.X, topLeft.Y - 5),
                HersheyFonts.HersheySimplex, 0.5, color, 1);
            
            // 绘制角度信息（如果有旋转）
            if (Math.Abs(match.Angle) > 0.1f)
            {
                string angleText = $"{match.Angle:F1}°";
                Cv2.PutText(image, angleText, new Point(topLeft.X, topLeft.Y + 15),
                    HersheyFonts.HersheySimplex, 0.4, color, 1);
            }
        }

        // 绘制状态信息
        string statusText = primaryMatch.IsValid
            ? $"OK: 找到 {validMatches.Count} 个匹配"
            : $"NG: 未找到匹配 (阈值 {minScore:F1}%)";

        var statusColor = primaryMatch.IsValid
            ? new Scalar(0, 255, 0)
            : new Scalar(0, 0, 255);

        Cv2.PutText(image, statusText, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, statusColor, 2);

        // 显示最佳匹配的详细信息
        if (primaryMatch.IsValid)
        {
            string detailText = $"最佳: ({primaryMatch.Position.X}, {primaryMatch.Position.Y}) 得分={primaryMatch.Score:F1}%";
            Cv2.PutText(image, detailText, new Point(10, 55),
                HersheyFonts.HersheySimplex, 0.5, statusColor, 1);
        }

        return (primaryMatch, validMatches);
    }

    // 重载：为保持向后兼容的 DrawMatches
    private (ImageProcessing.ShapeMatchResult primary, List<ImageProcessing.ShapeMatchResult> all) DrawMatches(
        Mat image,
        List<LineModMatchResult> matches,
        List<Template> templates,
        float minScore)
    {
        // 转换为统一格式
        var baseTemplate = templates.FirstOrDefault(t => t.PyramidLevel == 0);
        int templateWidth = baseTemplate?.Width ?? 100;
        int templateHeight = baseTemplate?.Height ?? 100;
        
        var convertedMatches = matches.Select(m => new ImageProcessing.ShapeMatchResult
        {
            IsValid = m.IsValid,
            Position = m.Position,
            Score = (float)(m.Score * 100.0),  // 转换为百分比
            Angle = (float)m.Angle,
            TemplateWidth = templateWidth,
            TemplateHeight = templateHeight
        }).ToList();
        
        return DrawShapeMatchResults(image, convertedMatches, minScore);
    }

    /// <summary>
    /// 构建缓存键
    /// </summary>
    private string BuildCacheKey(string? path, Mat? image, int pyramidLevels, int numFeatures, int angleRange = 0, int angleStep = 5)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return $"{path}_{pyramidLevels}_{numFeatures}_{angleRange}_{angleStep}";
        }

        if (image != null)
        {
            // 使用图像尺寸和通道数作为简单哈希
            return $"img_{image.Width}_{image.Height}_{image.Channels()}_{pyramidLevels}_{numFeatures}_{angleRange}_{angleStep}";
        }

        return $"default_{pyramidLevels}_{numFeatures}_{angleRange}_{angleStep}";
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
            return ValidationResult.Invalid("扩展因子 T 必须在 1-16 之间");
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
