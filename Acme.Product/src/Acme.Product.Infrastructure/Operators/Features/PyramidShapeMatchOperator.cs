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
        var angleRange = GetIntParam(@operator, "AngleRange", 0, min: 0, max: 180);
        var angleStep = GetIntParam(@operator, "AngleStep", 5, min: 1, max: 45);
        var pyramidLevels = GetIntParam(@operator, "PyramidLevels", 3, min: 1, max: 5);
        var weakThreshold = GetFloatParam(@operator, "WeakThreshold", 30.0f, min: 0.0f, max: 255.0f);
        var strongThreshold = GetFloatParam(@operator, "StrongThreshold", 60.0f, min: 0.0f, max: 255.0f);
        var numFeatures = GetIntParam(@operator, "NumFeatures", 150, min: 50, max: 8191);
        var spreadT = GetIntParam(@operator, "SpreadT", 4, min: 1, max: 16);
        var maxMatches = GetIntParam(@operator, "MaxMatches", 10, min: 1, max: 100);

        // 获取模板图像 (优先从输入获取)
        Mat? templateFromInput = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateFromInput = templateWrapper.GetMat().Clone();
        }

        using var srcImage = imageWrapper.GetMat();

        try
        {
            return RunCpuBoundWork(() =>
            {
                // 创建 LINEMOD 匹配器
                using var matcher = new LineModShapeMatcher
                {
                    WeakThreshold = weakThreshold,
                    StrongThreshold = strongThreshold,
                    NumFeatures = numFeatures,
                    SpreadT = spreadT,
                    PyramidLevels = pyramidLevels
                };

                // 获取或训练模板
                string cacheKey = BuildCacheKey(templatePath, templateFromInput, pyramidLevels, numFeatures, angleRange, angleStep);
                List<Template>? templates;

                lock (_cacheLock)
                {
                    if (!_templateCache.TryGetValue(cacheKey, out templates))
                    {
                        // 训练模板 (支持多角度旋转)
                        if (templateFromInput != null)
                        {
                            templates = matcher.Train(templateFromInput, null, angleRange, angleStep);
                        }
                        else if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                        {
                            using var templateImg = Cv2.ImRead(templatePath, ImreadModes.Color);
                            if (templateImg.Empty())
                            {
                                return OperatorExecutionOutput.Failure($"无法加载模板图像: {templatePath}");
                            }
                            templates = matcher.Train(templateImg, null, angleRange, angleStep);
                        }
                        else
                        {
                            return OperatorExecutionOutput.Failure("未提供模板图像或路径");
                        }

                        // 缓存模板
                        if (templates.Count > 0)
                        {
                            _templateCache[cacheKey] = templates;
                        }
                    }
                }

                if (templates == null || templates.Count == 0)
                {
                    return OperatorExecutionOutput.Failure("模板训练失败，未提取到有效特征点");
                }

                // 执行匹配
                var matches = matcher.Match(srcImage, minScore / 100.0f, maxMatches);

                // 创建结果图像
                using var resultImage = srcImage.Clone();

                // 绘制匹配结果
                var (primaryMatch, allMatches) = DrawMatches(resultImage, matches, templates, minScore);

                // 构建输出数据
                var outputData = new Dictionary<string, object>
                {
                    { "IsMatch", primaryMatch.IsValid },
                    { "Score", primaryMatch.Score },
                    { "X", primaryMatch.Position.X },
                    { "Y", primaryMatch.Position.Y },
                    { "Angle", primaryMatch.Angle },
                    { "MatchCount", allMatches.Count },
                    { "PyramidLevels", pyramidLevels }
                };

                // 添加所有匹配结果
                for (int i = 0; i < allMatches.Count && i < maxMatches; i++)
                {
                    var match = allMatches[i];
                    outputData[$"Match{i}_X"] = match.Position.X;
                    outputData[$"Match{i}_Y"] = match.Position.Y;
                    outputData[$"Match{i}_Score"] = match.Score;
                    outputData[$"Match{i}_Angle"] = match.Angle;
                }

                // 释放模板图像 (如果是从输入获取的)
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
    /// 在图像上绘制匹配结果
    /// </summary>
    private (LineModMatchResult primary, List<LineModMatchResult> all) DrawMatches(
        Mat image,
        List<LineModMatchResult> matches,
        List<Template> templates,
        float minScore)
    {
        var validMatches = matches.Where(m => m.IsValid).ToList();
        var primaryMatch = validMatches.FirstOrDefault();

        // 获取模板尺寸 (使用第一层)
        var baseTemplate = templates.FirstOrDefault(t => t.PyramidLevel == 0);
        int templateWidth = baseTemplate?.Width ?? 100;
        int templateHeight = baseTemplate?.Height ?? 100;

        // 绘制所有匹配结果
        for (int i = 0; i < validMatches.Count; i++)
        {
            var match = validMatches[i];
            var color = i == 0
                ? new Scalar(0, 255, 0)    // 最佳匹配 - 绿色
                : new Scalar(255, 255, 0); // 其他匹配 - 青色

            // 计算矩形框
            int halfW = templateWidth / 2;
            int halfH = templateHeight / 2;

            var topLeft = new Point(match.Position.X - halfW, match.Position.Y - halfH);
            var bottomRight = new Point(match.Position.X + halfW, match.Position.Y + halfH);

            // 绘制矩形框
            Cv2.Rectangle(image, topLeft, bottomRight, color, 2);

            // 绘制中心点
            Cv2.DrawMarker(image, match.Position, color, MarkerTypes.Cross, 20, 2);

            // 绘制得分
            string scoreText = $"{match.Score:F1}%";
            Cv2.PutText(image, scoreText, new Point(topLeft.X, topLeft.Y - 5),
                HersheyFonts.HersheySimplex, 0.5, color, 1);
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
