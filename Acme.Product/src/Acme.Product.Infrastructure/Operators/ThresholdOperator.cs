// ThresholdOperator.cs
// 阈值二值化算子
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 阈值二值化算子
/// </summary>
[OperatorMeta(
    DisplayName = "二值化",
    Description = "全局/自适应/Otsu 二值化分割，将图像转为前景/背景二值图，用于缺陷区域分离",
    Category = "预处理",
    IconName = "threshold",
    Keywords = new[] { "二值化", "阈值", "分割", "黑白", "Otsu", "Threshold", "Binarize", "Segmentation" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("Threshold", "阈值", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MaxValue", "最大值", "double", DefaultValue = 255.0, Min = 0.0, Max = 255.0)]
[OperatorParam("Type", "类型", "enum", DefaultValue = "0", Options = new[] { "0|Binary", "1|Binary Inv", "2|Trunc", "3|To Zero", "4|To Zero Inv", "8|Otsu", "16|Triangle" })]
[OperatorParam("UseOtsu", "使用Otsu", "bool", DefaultValue = false)]
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

        var src = imageWrapper.GetMat();
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
        var dst = new Mat();
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
