// AdaptiveThresholdOperator.cs
// 自适应阈值算子 - 支持Mean和Gaussian自适应阈值
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 自适应阈值算子 - 支持Mean和Gaussian自适应阈值
/// </summary>
[OperatorMeta(
    DisplayName = "自适应阈值",
    Description = "Mean和Gaussian自适应阈值",
    Category = "预处理",
    IconName = "adaptive-threshold",
    Keywords = new[] { "自适应", "阈值", "局部", "Adaptive", "Local threshold", "Gaussian" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "输出图像", PortDataType.Image)]
[OperatorParam("MaxValue", "最大值", "double", DefaultValue = 255.0, Min = 0.0, Max = 255.0)]
[OperatorParam("AdaptiveMethod", "自适应方法", "enum", DefaultValue = "Gaussian", Options = new[] { "Gaussian|高斯加权", "Mean|均值" })]
[OperatorParam("ThresholdType", "阈值类型", "enum", DefaultValue = "Binary", Options = new[] { "Binary|二值化", "BinaryInv|反二值化" })]
[OperatorParam("BlockSize", "块大小", "int", DefaultValue = 11, Min = 3, Max = 99)]
[OperatorParam("C", "常数C", "double", DefaultValue = 2.0, Min = -100.0, Max = 100.0)]
public class AdaptiveThresholdOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.AdaptiveThreshold;

    public AdaptiveThresholdOperator(ILogger<AdaptiveThresholdOperator> logger) : base(logger)
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

        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0, min: 0, max: 255);
        var adaptiveMethod = GetStringParam(@operator, "AdaptiveMethod", "Gaussian");
        var thresholdType = GetStringParam(@operator, "ThresholdType", "Binary");
        var blockSize = GetIntParam(@operator, "BlockSize", 11, min: 3, max: 51);
        var c = GetDoubleParam(@operator, "C", 2.0);

        // 确保blockSize为奇数
        if (blockSize % 2 == 0)
            blockSize++;

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 确保输入为灰度图（AdaptiveThreshold 要求单通道 CV_8UC1）
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        else
            src.CopyTo(gray);

        var adaptiveType = adaptiveMethod.ToLower() switch
        {
            "mean" => AdaptiveThresholdTypes.MeanC,
            "gaussian" => AdaptiveThresholdTypes.GaussianC,
            _ => AdaptiveThresholdTypes.GaussianC
        };

        var threshType = thresholdType.ToLower() switch
        {
            "binary" => ThresholdTypes.Binary,
            "binaryinv" => ThresholdTypes.BinaryInv,
            _ => ThresholdTypes.Binary
        };

        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, maxValue, adaptiveType, threshType, blockSize, c);

        // 【关键修复】将单通道二值图像转换为3通道BGR格式，确保浏览器兼容性
        // 单通道PNG在某些浏览器/Canvas中显示为白色或黑色
        var dst = new Mat();
        Cv2.CvtColor(binary, dst, ColorConversionCodes.GRAY2BGR);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "AdaptiveMethod", adaptiveMethod },
            { "BlockSize", blockSize },
            { "C", c }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0);
        if (maxValue < 0 || maxValue > 255)
            return ValidationResult.Invalid("最大值必须在 0-255 之间");

        var blockSize = GetIntParam(@operator, "BlockSize", 11);
        if (blockSize < 3 || blockSize > 51)
            return ValidationResult.Invalid("块大小必须在 3-51 之间");

        var adaptiveMethod = GetStringParam(@operator, "AdaptiveMethod", "Gaussian").ToLower();
        var validMethods = new[] { "mean", "gaussian" };
        if (!validMethods.Contains(adaptiveMethod))
            return ValidationResult.Invalid($"不支持的自适应方法: {adaptiveMethod}");

        var thresholdType = GetStringParam(@operator, "ThresholdType", "Binary").ToLower();
        var validTypes = new[] { "binary", "binaryinv" };
        if (!validTypes.Contains(thresholdType))
            return ValidationResult.Invalid($"不支持的阈值类型: {thresholdType}");

        return ValidationResult.Valid();
    }
}
