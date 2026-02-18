// ImageCropOperator.cs
// 图像裁剪算子 - 提取ROI区域
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像裁剪算子 - 提取ROI区域
/// </summary>
public class ImageCropOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageCrop;

    public ImageCropOperator(ILogger<ImageCropOperator> logger) : base(logger)
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

        var x = GetIntParam(@operator, "X", 0, min: 0);
        var y = GetIntParam(@operator, "Y", 0, min: 0);
        var width = GetIntParam(@operator, "Width", 100, min: 1);
        var height = GetIntParam(@operator, "Height", 100, min: 1);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 边界检查
        if (x >= src.Width) x = src.Width - 1;
        if (y >= src.Height) y = src.Height - 1;
        if (x + width > src.Width) width = src.Width - x;
        if (y + height > src.Height) height = src.Height - y;

        var roi = new Rect(x, y, width, height);
        using var cropped = new Mat(src, roi);
        using var dst = cropped.Clone();

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var x = GetIntParam(@operator, "X", 0);
        var y = GetIntParam(@operator, "Y", 0);
        var width = GetIntParam(@operator, "Width", 100);
        var height = GetIntParam(@operator, "Height", 100);

        if (width < 1)
            return ValidationResult.Invalid("裁剪宽度必须大于0");
        if (height < 1)
            return ValidationResult.Invalid("裁剪高度必须大于0");
        if (x < 0)
            return ValidationResult.Invalid("起始X不能为负数");
        if (y < 0)
            return ValidationResult.Invalid("起始Y不能为负数");

        return ValidationResult.Valid();
    }
}
