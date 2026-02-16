using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Canny边缘检测算子
/// </summary>
public class CannyEdgeOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.EdgeDetection;

    public CannyEdgeOperator(ILogger<CannyEdgeOperator> logger) : base(logger) { }

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
        var threshold1 = GetDoubleParam(@operator, "Threshold1", 50.0, 0, 255);
        var threshold2 = GetDoubleParam(@operator, "Threshold2", 150.0, 0, 255);
        var enableGaussianBlur = GetBoolParam(@operator, "EnableGaussianBlur", true);
        var gaussianKernelSize = GetIntParam(@operator, "GaussianKernelSize", 5, 1, 31);
        var apertureSize = GetIntParam(@operator, "ApertureSize", 3, 3, 7);
        var l2Gradient = GetBoolParam(@operator, "L2Gradient", false);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 确保输入为灰度图（Canny 标准流程要求单通道）
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        else
            src.CopyTo(gray);

        using var processedSrc = new Mat();

        // 可选的高斯模糊预处理（Canny算法标准建议）
        if (enableGaussianBlur)
        {
            // 确保核大小为奇数
            if (gaussianKernelSize % 2 == 0)
                gaussianKernelSize++;
            Cv2.GaussianBlur(gray, processedSrc, new Size(gaussianKernelSize, gaussianKernelSize), 1.0);
        }
        else
        {
            gray.CopyTo(processedSrc);
        }

        using var dst = new Mat();
        Cv2.Canny(processedSrc, dst, threshold1, threshold2, apertureSize, l2Gradient);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Edges", dst }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold1 = GetDoubleParam(@operator, "Threshold1", 50.0);
        var threshold2 = GetDoubleParam(@operator, "Threshold2", 150.0);

        if (threshold1 < 0 || threshold1 > 255)
            return ValidationResult.Invalid("阈值1必须在 0-255 之间");

        if (threshold2 < 0 || threshold2 > 255)
            return ValidationResult.Invalid("阈值2必须在 0-255 之间");

        return ValidationResult.Valid();
    }
}
