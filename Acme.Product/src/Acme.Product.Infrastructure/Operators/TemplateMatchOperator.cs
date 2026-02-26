// TemplateMatchOperator.cs
// 模板匹配算子 - 在图像中查找模板位置
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 模板匹配算子 - 在图像中查找模板位置
/// </summary>
[OperatorMeta(
    DisplayName = "模板匹配",
    Description = "NCC/SQDiff 模板匹配，用于定位目标位置和缺失检测",
    Category = "匹配定位",
    IconName = "template",
    Keywords = new[] { "模板匹配", "定位", "找图", "特征匹配", "关联定位", "Template", "Match", "Locate" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Position", "匹配位置", PortDataType.Point)]
[OutputPort("Score", "匹配分数", PortDataType.Float)]
[OutputPort("IsMatch", "是否匹配", PortDataType.Boolean)]
[OperatorParam("Method", "匹配方法", "enum", DefaultValue = "NCC", Options = new[] { "NCC|归一化相关 (NCC)", "SQDiff|平方差 (SQDiff)" })]
[OperatorParam("Threshold", "匹配分数阈值", "double", DefaultValue = 0.8, Min = 0.1, Max = 1.0)]
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

        if (!TryGetInputValue<byte[]>(inputs, "Template", out var templateData) || templateData == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像"));
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 0.8, min: 0, max: 1);
        var method = GetStringParam(@operator, "Method", "CCoeffNormed");

        var src = imageWrapper.GetMat();
        using var template = Cv2.ImDecode(templateData, ImreadModes.Color);

        if (src.Empty() || template.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码图像"));
        }

        if (template.Width > src.Width || template.Height > src.Height)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("模板尺寸不能大于源图像"));
        }

        // 选择匹配方法
        var matchMethod = method.ToLower() switch
        {
            "sqdiff" => TemplateMatchModes.SqDiff,
            "sqdiffnormed" => TemplateMatchModes.SqDiffNormed,
            "ccorr" => TemplateMatchModes.CCorr,
            "ccorrnormed" => TemplateMatchModes.CCorrNormed,
            "ccoeff" => TemplateMatchModes.CCoeff,
            "ccoeffnormed" => TemplateMatchModes.CCoeffNormed,
            _ => TemplateMatchModes.CCoeffNormed
        };

        // 执行模板匹配
        using var result = new Mat();
        Cv2.MatchTemplate(src, template, result, matchMethod);

        // 查找最佳匹配位置
        Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out Point minLoc, out Point maxLoc);

        // 根据方法确定最佳匹配值和位置
        bool isSqDiff = matchMethod == TemplateMatchModes.SqDiff || matchMethod == TemplateMatchModes.SqDiffNormed;
        double matchValue = isSqDiff ? minVal : maxVal;
        Point matchLoc = isSqDiff ? minLoc : maxLoc;

        // 归一化匹配值到0-1范围
        double normalizedScore = isSqDiff ? 1.0 - matchValue : matchValue;

        // 绘制结果
        using var resultImg = src.Clone();
        bool found = normalizedScore >= threshold;

        if (found)
        {
            Cv2.Rectangle(resultImg, matchLoc, new Point(matchLoc.X + template.Width, matchLoc.Y + template.Height),
                new Scalar(0, 255, 0), 2);
            Cv2.PutText(resultImg, $"Score: {normalizedScore:F3}",
                new Point(matchLoc.X, matchLoc.Y - 10),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImg, new Dictionary<string, object>
        {
            { "Found", found },
            { "IsMatch", found },
            { "Score", normalizedScore },
            { "Position", matchLoc },
            { "X", matchLoc.X },
            { "Y", matchLoc.Y },
            { "TemplateWidth", template.Width },
            { "TemplateHeight", template.Height }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 0.8);
        if (threshold < 0 || threshold > 1)
        {
            return ValidationResult.Invalid("阈值必须在 0-1 之间");
        }

        var method = GetStringParam(@operator, "Method", "CCoeffNormed").ToLower();
        var validMethods = new[] { "sqdiff", "sqdiffnormed", "ccorr", "ccorrnormed", "ccoeff", "ccoeffnormed" };
        if (!validMethods.Contains(method))
        {
            return ValidationResult.Invalid($"不支持的匹配方法: {method}");
        }

        return ValidationResult.Valid();
    }
}
