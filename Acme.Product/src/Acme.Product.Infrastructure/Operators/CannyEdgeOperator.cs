// CannyEdgeOperator.cs
// Canny边缘检测算子
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Canny边缘检测算子
/// </summary>
[OperatorMeta(
    DisplayName = "边缘检测",
    Description = "利用 Canny/Sobel 等算法检测图像边缘，用于尺寸测量和缺陷定位前的轮廓提取",
    Category = "特征提取",
    IconName = "edge",
    Keywords = new[] { "边缘", "轮廓提取", "Canny", "Sobel", "边界", "找边", "Edge", "Contour extraction" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OutputPort("Edges", "边缘", PortDataType.Image)]
[OperatorParam("Threshold1", "低阈值", "double", DefaultValue = 50.0, Min = 0.0, Max = 255.0)]
[OperatorParam("Threshold2", "高阈值", "double", DefaultValue = 150.0, Min = 0.0, Max = 255.0)]
[OperatorParam("EnableGaussianBlur", "启用高斯模糊", "bool", DefaultValue = true)]
[OperatorParam("GaussianKernelSize", "高斯核大小", "int", DefaultValue = 5, Min = 3, Max = 15)]
[OperatorParam("ApertureSize", "Sobel孔径", "enum", DefaultValue = "3", Options = new[] { "3|3", "5|5", "7|7" })]
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

        var src = imageWrapper.GetMat();
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

        var dst = new Mat();
        Cv2.Canny(processedSrc, dst, threshold1, threshold2, apertureSize, l2Gradient);

        // 保持 "Edges" 端口可用，同时避免将 OpenCvSharp.Mat 直接放入输出字典（会在后续 JSON 序列化阶段触发 DataPointer 错误）
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Edges", dst.ToBytes(".png") }
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
