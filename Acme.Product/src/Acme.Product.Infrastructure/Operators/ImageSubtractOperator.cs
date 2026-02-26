// ImageSubtractOperator.cs
// 图像减法算子 - 差异检测
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像减法算子 - 差异检测
/// 【第三优先级】图像预处理算子扩展
/// </summary>
[OperatorMeta(
    DisplayName = "图像减法",
    Description = "差异检测",
    Category = "预处理",
    IconName = "subtract"
)]
[InputPort("Image1", "图像1", PortDataType.Image, IsRequired = true)]
[InputPort("Image2", "图像2", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "差异图像", PortDataType.Image)]
[OutputPort("MinDifference", "最小差异", PortDataType.Float)]
[OutputPort("MaxDifference", "最大差异", PortDataType.Float)]
[OutputPort("MeanDifference", "平均差异", PortDataType.Float)]
[OperatorParam("AbsoluteDiff", "绝对差异", "bool", Description = "使用绝对差异或简单相减", DefaultValue = true)]
public class ImageSubtractOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageSubtract;

    public ImageSubtractOperator(ILogger<ImageSubtractOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image1", out var image1Wrapper) || image1Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第一幅输入图像(Image1)"));
        }

        if (!TryGetInputImage(inputs, "Image2", out var image2Wrapper) || image2Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第二幅输入图像(Image2)"));
        }

        var absoluteDiff = GetBoolParam(@operator, "AbsoluteDiff", true);

        var src1 = image1Wrapper.GetMat();
        var src2 = image2Wrapper.GetMat();

        if (src1.Empty() || src2.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = new Mat();

        // 确保两幅图像尺寸相同
        if (src1.Size() != src2.Size())
        {
            using var resized2 = new Mat();
            Cv2.Resize(src2, resized2, src1.Size());

            if (absoluteDiff)
            {
                Cv2.Absdiff(src1, resized2, dst);
            }
            else
            {
                Cv2.Subtract(src1, resized2, dst);
            }
        }
        else
        {
            if (absoluteDiff)
            {
                Cv2.Absdiff(src1, src2, dst);
            }
            else
            {
                Cv2.Subtract(src1, src2, dst);
            }
        }

        // 计算差异统计
        double minVal = 0, maxVal = 0;
        Cv2.MinMaxLoc(dst, out minVal, out maxVal);
        var mean = Cv2.Mean(dst);
        double diffSum = mean.Val0 + mean.Val1 + mean.Val2;

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "AbsoluteDiff", absoluteDiff },
            { "MinDifference", minVal },
            { "MaxDifference", maxVal },
            { "MeanDifference", diffSum / src1.Channels() }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }
}
