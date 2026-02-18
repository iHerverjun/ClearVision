// BilateralFilterOperator.cs
// 双边滤波算子 - 边缘保留的平滑滤波
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 双边滤波算子 - 边缘保留的平滑滤波
/// </summary>
public class BilateralFilterOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BilateralFilter;

    public BilateralFilterOperator(ILogger<BilateralFilterOperator> logger) : base(logger)
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

        var diameter = GetIntParam(@operator, "Diameter", 9, min: 1, max: 25);
        var sigmaColor = GetDoubleParam(@operator, "SigmaColor", 75.0, min: 1, max: 255);
        var sigmaSpace = GetDoubleParam(@operator, "SigmaSpace", 75.0, min: 1, max: 255);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        using var dst = new Mat();
        Cv2.BilateralFilter(src, dst, diameter, sigmaColor, sigmaSpace);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var diameter = GetIntParam(@operator, "Diameter", 9);
        var sigmaColor = GetDoubleParam(@operator, "SigmaColor", 75.0);
        var sigmaSpace = GetDoubleParam(@operator, "SigmaSpace", 75.0);

        if (diameter < 1 || diameter > 25)
        {
            return ValidationResult.Invalid("直径必须在 1-25 之间");
        }
        if (sigmaColor < 1 || sigmaColor > 255)
        {
            return ValidationResult.Invalid("色彩Sigma必须在 1-255 之间");
        }
        if (sigmaSpace < 1 || sigmaSpace > 255)
        {
            return ValidationResult.Invalid("空间Sigma必须在 1-255 之间");
        }
        return ValidationResult.Valid();
    }
}
