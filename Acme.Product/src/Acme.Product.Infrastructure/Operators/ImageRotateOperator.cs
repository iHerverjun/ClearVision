// ImageRotateOperator.cs
// 图像旋转算子 - 任意角度旋转
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像旋转算子 - 任意角度旋转
/// </summary>
public class ImageRotateOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageRotate;

    public ImageRotateOperator(ILogger<ImageRotateOperator> logger) : base(logger)
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

        var angle = GetDoubleParam(@operator, "Angle", 0.0, min: -360, max: 360);
        var centerX = GetIntParam(@operator, "CenterX", -1);
        var centerY = GetIntParam(@operator, "CenterY", -1);
        var scale = GetDoubleParam(@operator, "Scale", 1.0, min: 0.1, max: 10.0);
        var autoResize = GetBoolParam(@operator, "AutoResize", true);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var center = new Point2f(
            centerX < 0 ? src.Width / 2f : centerX,
            centerY < 0 ? src.Height / 2f : centerY
        );

        using var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, scale);

        Size dstSize;
        if (autoResize)
        {
            double cos = Math.Abs(Math.Cos(angle * Math.PI / 180.0));
            double sin = Math.Abs(Math.Sin(angle * Math.PI / 180.0));
            int newWidth = (int)(src.Width * cos + src.Height * sin);
            int newHeight = (int)(src.Width * sin + src.Height * cos);
            dstSize = new Size(newWidth, newHeight);

            rotationMatrix.At<double>(0, 2) += (dstSize.Width - src.Width) / 2.0;
            rotationMatrix.At<double>(1, 2) += (dstSize.Height - src.Height) / 2.0;
        }
        else
        {
            dstSize = new Size(src.Width, src.Height);
        }

        using var dst = new Mat();
        Cv2.WarpAffine(src, dst, rotationMatrix, dstSize, InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "Angle", angle },
            { "Scale", scale }
        };
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var angle = GetDoubleParam(@operator, "Angle", 0.0);
        var scale = GetDoubleParam(@operator, "Scale", 1.0);

        if (angle < -360 || angle > 360)
            return ValidationResult.Invalid("旋转角度必须在 -360 到 360 之间");
        if (scale < 0.1 || scale > 10.0)
            return ValidationResult.Invalid("缩放比例必须在 0.1-10.0 之间");

        return ValidationResult.Valid();
    }
}
