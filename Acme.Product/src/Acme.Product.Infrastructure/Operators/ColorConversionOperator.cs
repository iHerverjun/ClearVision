// ColorConversionOperator.cs
// 颜色空间转换算子 - 支持BGR// 功能实现GRAY// 功能实现HSV// 功能实现Lab// 功能实现YUV等颜色空间转换
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 颜色空间转换算子 - 支持BGR/GRAY/HSV/Lab/YUV等颜色空间转换
/// </summary>
[OperatorMeta(
    DisplayName = "颜色空间转换",
    Description = "BGR/GRAY/HSV/Lab/YUV等颜色空间转换",
    Category = "预处理",
    IconName = "color-convert",
    Keywords = new[] { "颜色", "色彩", "灰度", "HSV", "Lab", "转换", "Color", "Convert", "Gray" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "输出图像", PortDataType.Image)]
[OperatorParam("ConversionCode", "转换类型", "enum", DefaultValue = "BGR2GRAY", Options = new[] { "BGR2GRAY|BGR转灰度", "BGR2HSV|BGR转HSV", "BGR2Lab|BGR转Lab", "BGR2YUV|BGR转YUV", "GRAY2BGR|灰度转BGR", "HSV2BGR|HSV转BGR" })]
[OperatorParam("SourceChannels", "源通道数", "int", DefaultValue = 3, Min = 1, Max = 4, Description = "输入图像的通道数，用于验证转换类型兼容性")]
public class ColorConversionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ColorConversion;

    public ColorConversionOperator(ILogger<ColorConversionOperator> logger) : base(logger)
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

        var conversionCode = GetStringParam(@operator, "ConversionCode", "BGR2GRAY");
        var srcChannels = GetIntParam(@operator, "SourceChannels", 3, min: 1, max: 4);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 解析颜色转换代码
        var colorCode = conversionCode.ToUpper() switch
        {
            "BGR2GRAY" => ColorConversionCodes.BGR2GRAY,
            "GRAY2BGR" => ColorConversionCodes.GRAY2BGR,
            "BGR2HSV" => ColorConversionCodes.BGR2HSV,
            "HSV2BGR" => ColorConversionCodes.HSV2BGR,
            "BGR2Lab" => ColorConversionCodes.BGR2Lab,
            "Lab2BGR" => ColorConversionCodes.Lab2BGR,
            "BGR2YUV" => ColorConversionCodes.BGR2YUV,
            "YUV2BGR" => ColorConversionCodes.YUV2BGR,
            "BGR2RGB" => ColorConversionCodes.BGR2RGB,
            "RGB2BGR" => ColorConversionCodes.RGB2BGR,
            "BGR2RGBA" => ColorConversionCodes.BGR2RGBA,
            "BGR2XYZ" => ColorConversionCodes.BGR2XYZ,
            "XYZ2BGR" => ColorConversionCodes.XYZ2BGR,
            "BGR2HLS" => ColorConversionCodes.BGR2HLS,
            "HLS2BGR" => ColorConversionCodes.HLS2BGR,
            _ => ColorConversionCodes.BGR2GRAY
        };

        var dst = new Mat();
        Cv2.CvtColor(src, dst, colorCode);

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "Channels", dst.Channels() },
            { "ConversionCode", conversionCode }
        };
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var conversionCode = GetStringParam(@operator, "ConversionCode", "BGR2GRAY").ToUpper();
        var validCodes = new[] 
        { 
            "BGR2GRAY", "GRAY2BGR", "BGR2HSV", "HSV2BGR",
            "BGR2Lab", "Lab2BGR", "BGR2YUV", "YUV2BGR",
            "BGR2RGB", "RGB2BGR", "BGR2RGBA", "BGR2XYZ",
            "XYZ2BGR", "BGR2HLS", "HLS2BGR"
        };
        
        if (!validCodes.Contains(conversionCode))
        {
            return ValidationResult.Invalid($"不支持的颜色转换代码: {conversionCode}");
        }

        var srcChannels = GetIntParam(@operator, "SourceChannels", 3);
        if (srcChannels is not (1 or 3 or 4))
        {
            return ValidationResult.Invalid("源通道数必须是1、3或4");
        }

        return ValidationResult.Valid();
    }
}
