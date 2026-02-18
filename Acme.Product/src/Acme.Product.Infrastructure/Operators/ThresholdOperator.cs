// ThresholdOperator.cs
// 阈值二值化算子
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 阈值二值化算子
/// </summary>
public class ThresholdOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Thresholding;

    public ThresholdOperator(ILogger<ThresholdOperator> logger) : base(logger)
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

        var thresh = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var maxval = GetDoubleParam(@operator, "MaxValue", 255.0, min: 0, max: 255);
        var type = GetIntParam(@operator, "Type", 0);
        var useOtsu = GetBoolParam(@operator, "UseOtsu", false);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 确保输入为灰度图（Threshold/OTSU 要求单通道 CV_8UC1）
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        else
            src.CopyTo(gray);

        using var binary = new Mat();

        var thresholdType = (ThresholdTypes)type;
        if (useOtsu)
        {
            thresholdType |= ThresholdTypes.Otsu;
        }

        var actualThreshold = Cv2.Threshold(gray, binary, thresh, maxval, thresholdType);

        // 【关键修复】将单通道二值图像转换为3通道BGR格式，确保浏览器兼容性
        // 单通道PNG在某些浏览器/Canvas中显示为白色或黑色
        using var dst = new Mat();
        Cv2.CvtColor(binary, dst, ColorConversionCodes.GRAY2BGR);

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "ActualThreshold", actualThreshold }
        };

        if (useOtsu)
        {
            additionalData["OtsuThreshold"] = actualThreshold;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var thresh = GetDoubleParam(@operator, "Threshold", 127.0);
        if (thresh < 0 || thresh > 255)
            return ValidationResult.Invalid("阈值必须在 0-255 之间");

        return ValidationResult.Valid();
    }
}
