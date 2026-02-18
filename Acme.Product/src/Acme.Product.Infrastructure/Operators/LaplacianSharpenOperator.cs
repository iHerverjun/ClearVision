// LaplacianSharpenOperator.cs
// 拉普拉斯锐化算子 - 边缘增强
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 拉普拉斯锐化算子 - 边缘增强
/// 【第三优先级】图像预处理算子扩展
/// </summary>
public class LaplacianSharpenOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LaplacianSharpen;

    public LaplacianSharpenOperator(ILogger<LaplacianSharpenOperator> logger) : base(logger)
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

        // 获取参数
        var kernelSize = GetIntParam(@operator, "KernelSize", 3, min: 1, max: 7);
        var scale = GetDoubleParam(@operator, "Scale", 1.0, min: 0.1, max: 10.0);
        var delta = GetDoubleParam(@operator, "Delta", 0, min: -100, max: 100);
        var sharpenStrength = GetDoubleParam(@operator, "SharpenStrength", 1.0, min: 0, max: 5.0);

        // 确保核大小为奇数
        if (kernelSize % 2 == 0) kernelSize++;

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        using var laplacian = new Mat();
        using var dst = new Mat();

        // 转换为灰度图进行拉普拉斯运算（如果是彩色图）
        if (src.Channels() == 3)
        {
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            
            // 拉普拉斯边缘检测
            Cv2.Laplacian(gray, laplacian, MatType.CV_32F, kernelSize, scale, delta);
            
            // 转回8位
            Cv2.ConvertScaleAbs(laplacian, laplacian);
            
            // 锐化：原图 + 边缘 * 强度
            using var laplacian3C = new Mat();
            Cv2.CvtColor(laplacian, laplacian3C, ColorConversionCodes.GRAY2BGR);
            
            // 使用加权叠加：dst = src + strength * laplacian
            Cv2.AddWeighted(src, 1.0, laplacian3C, sharpenStrength, 0, dst);
        }
        else
        {
            // 单通道图像直接处理
            Cv2.Laplacian(src, laplacian, MatType.CV_32F, kernelSize, scale, delta);
            Cv2.ConvertScaleAbs(laplacian, laplacian);
            Cv2.AddWeighted(src, 1.0, laplacian, sharpenStrength, 0, dst);
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "KernelSize", kernelSize },
            { "Scale", scale },
            { "SharpenStrength", sharpenStrength }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelSize = GetIntParam(@operator, "KernelSize", 3);
        if (kernelSize < 1 || kernelSize > 7)
            return ValidationResult.Invalid("核大小必须在 1-7 之间");

        var scale = GetDoubleParam(@operator, "Scale", 1.0);
        if (scale < 0.1 || scale > 10.0)
            return ValidationResult.Invalid("缩放因子必须在 0.1-10.0 之间");

        var sharpenStrength = GetDoubleParam(@operator, "SharpenStrength", 1.0);
        if (sharpenStrength < 0 || sharpenStrength > 5.0)
            return ValidationResult.Invalid("锐化强度必须在 0-5.0 之间");

        return ValidationResult.Valid();
    }
}
