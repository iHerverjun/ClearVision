// ImageBlendOperator.cs
// 图像融合算子 - 加权混合// 功能实现透明叠加
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像融合算子 - 加权混合/透明叠加
/// 【第三优先级】图像预处理算子扩展
/// </summary>
public class ImageBlendOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageBlend;

    public ImageBlendOperator(ILogger<ImageBlendOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Background", out var bgWrapper) || bgWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供背景图像(Background)"));
        }

        if (!TryGetInputImage(inputs, "Foreground", out var fgWrapper) || fgWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供前景图像(Foreground)"));
        }

        // 获取参数
        var alpha = GetDoubleParam(@operator, "Alpha", 0.5, min: 0, max: 1.0); // 背景权重
        var beta = GetDoubleParam(@operator, "Beta", 0.5, min: 0, max: 1.0);   // 前景权重
        var gamma = GetDoubleParam(@operator, "Gamma", 0, min: -255, max: 255); // 亮度偏移

        using var background = bgWrapper.GetMat();
        using var foreground = fgWrapper.GetMat();

        if (background.Empty() || foreground.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = new Mat();

        // 调整前景图像大小以匹配背景
        if (background.Size() != foreground.Size())
        {
            using var resizedFg = new Mat();
            Cv2.Resize(foreground, resizedFg, background.Size());
            Cv2.AddWeighted(background, alpha, resizedFg, beta, gamma, dst);
        }
        else
        {
            Cv2.AddWeighted(background, alpha, foreground, beta, gamma, dst);
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Alpha", alpha },
            { "Beta", beta },
            { "Gamma", gamma }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var alpha = GetDoubleParam(@operator, "Alpha", 0.5);
        if (alpha < 0 || alpha > 1.0)
            return ValidationResult.Invalid("背景权重(Alpha)必须在 0-1.0 之间");

        var beta = GetDoubleParam(@operator, "Beta", 0.5);
        if (beta < 0 || beta > 1.0)
            return ValidationResult.Invalid("前景权重(Beta)必须在 0-1.0 之间");

        return ValidationResult.Valid();
    }
}
