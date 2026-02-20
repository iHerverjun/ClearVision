// PerspectiveTransformOperator.cs
// 透视变换算子 - 四边形透视校正
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 透视变换算子 - 四边形透视校正
/// </summary>
public class PerspectiveTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PerspectiveTransform;

    public PerspectiveTransformOperator(ILogger<PerspectiveTransformOperator> logger) : base(logger)
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

        // 获取源点参数 (左上、右上、右下、左下顺序)
        var srcX1 = GetDoubleParam(@operator, "SrcX1", 0.0);
        var srcY1 = GetDoubleParam(@operator, "SrcY1", 0.0);
        var srcX2 = GetDoubleParam(@operator, "SrcX2", 100.0);
        var srcY2 = GetDoubleParam(@operator, "SrcY2", 0.0);
        var srcX3 = GetDoubleParam(@operator, "SrcX3", 100.0);
        var srcY3 = GetDoubleParam(@operator, "SrcY3", 100.0);
        var srcX4 = GetDoubleParam(@operator, "SrcX4", 0.0);
        var srcY4 = GetDoubleParam(@operator, "SrcY4", 100.0);

        // 获取目标点参数
        var dstX1 = GetDoubleParam(@operator, "DstX1", 0.0);
        var dstY1 = GetDoubleParam(@operator, "DstY1", 0.0);
        var dstX2 = GetDoubleParam(@operator, "DstX2", 640.0);
        var dstY2 = GetDoubleParam(@operator, "DstY2", 0.0);
        var dstX3 = GetDoubleParam(@operator, "DstX3", 640.0);
        var dstY3 = GetDoubleParam(@operator, "DstY3", 480.0);
        var dstX4 = GetDoubleParam(@operator, "DstX4", 0.0);
        var dstY4 = GetDoubleParam(@operator, "DstY4", 480.0);

        // 输出尺寸
        var outputWidth = GetIntParam(@operator, "OutputWidth", 640, min: 1, max: 8192);
        var outputHeight = GetIntParam(@operator, "OutputHeight", 480, min: 1, max: 8192);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 定义源点和目标点
        var srcPoints = new Point2f[]
        {
            new((float)srcX1, (float)srcY1),
            new((float)srcX2, (float)srcY2),
            new((float)srcX3, (float)srcY3),
            new((float)srcX4, (float)srcY4)
        };

        var dstPoints = new Point2f[]
        {
            new((float)dstX1, (float)dstY1),
            new((float)dstX2, (float)dstY2),
            new((float)dstX3, (float)dstY3),
            new((float)dstX4, (float)dstY4)
        };

        // 计算透视变换矩阵
        using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);

        // 执行透视变换
        var dst = new Mat();
        Cv2.WarpPerspective(src, dst, perspectiveMatrix, new Size(outputWidth, outputHeight), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var outputWidth = GetIntParam(@operator, "OutputWidth", 640);
        var outputHeight = GetIntParam(@operator, "OutputHeight", 480);

        if (outputWidth < 1 || outputWidth > 8192)
        {
            return ValidationResult.Invalid("输出宽度必须在 1-8192 之间");
        }
        if (outputHeight < 1 || outputHeight > 8192)
        {
            return ValidationResult.Invalid("输出高度必须在 1-8192 之间");
        }
        return ValidationResult.Valid();
    }
}
