// ImageResizeOperator.cs
// 图像缩放算子 - 调整图像尺寸
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像缩放算子 - 调整图像尺寸
/// </summary>
[OperatorMeta(
    DisplayName = "图像缩放",
    Description = "调整图像尺寸",
    Category = "预处理",
    IconName = "resize",
    Keywords = new[] { "缩放", "放大", "缩小", "尺寸", "Resize", "Scale", "Zoom" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("Width", "目标宽度", "int", DefaultValue = 640, Min = 1, Max = 8192)]
[OperatorParam("Height", "目标高度", "int", DefaultValue = 480, Min = 1, Max = 8192)]
[OperatorParam("ScaleFactor", "缩放比例", "double", DefaultValue = 1.0, Min = 0.01, Max = 10.0)]
[OperatorParam("Interpolation", "插值方法", "enum", DefaultValue = "Linear", Options = new[] { "Nearest|最近邻", "Linear|双线性", "Cubic|三次", "Area|区域" })]
[OperatorParam("UseScale", "使用比例", "bool", DefaultValue = false)]
public class ImageResizeOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageResize;

    public ImageResizeOperator(ILogger<ImageResizeOperator> logger) : base(logger)
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

        var targetWidth = GetIntParam(@operator, "Width", 640, min: 1, max: 8192);
        var targetHeight = GetIntParam(@operator, "Height", 480, min: 1, max: 8192);
        var scaleFactor = GetDoubleParam(@operator, "ScaleFactor", 1.0, min: 0.01, max: 10.0);
        var interpolation = GetStringParam(@operator, "Interpolation", "Linear");
        var useScale = GetBoolParam(@operator, "UseScale", false);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = new Mat();
        InterpolationFlags interpFlag = interpolation.ToLower() switch
        {
            "nearest" => InterpolationFlags.Nearest,
            "linear" => InterpolationFlags.Linear,
            "cubic" => InterpolationFlags.Cubic,
            "area" => InterpolationFlags.Area,
            _ => InterpolationFlags.Linear
        };

        if (useScale)
        {
            Cv2.Resize(src, dst, new Size(), scaleFactor, scaleFactor, interpFlag);
        }
        else
        {
            Cv2.Resize(src, dst, new Size(targetWidth, targetHeight), 0, 0, interpFlag);
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var width = GetIntParam(@operator, "Width", 640);
        var height = GetIntParam(@operator, "Height", 480);
        var scaleFactor = GetDoubleParam(@operator, "ScaleFactor", 1.0);

        if (width < 1 || width > 8192)
        {
            return ValidationResult.Invalid("目标宽度必须在 1-8192 之间");
        }
        if (height < 1 || height > 8192)
        {
            return ValidationResult.Invalid("目标高度必须在 1-8192 之间");
        }
        if (scaleFactor < 0.01 || scaleFactor > 10.0)
        {
            return ValidationResult.Invalid("缩放比例必须在 0.01-10.0 之间");
        }
        return ValidationResult.Valid();
    }
}
