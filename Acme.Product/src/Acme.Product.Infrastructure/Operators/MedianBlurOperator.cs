// MedianBlurOperator.cs
// 中值滤波算子 - 有效去除椒盐噪声同时保留边缘
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Acme.Product.Infrastructure.Memory;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 中值滤波算子 - 有效去除椒盐噪声同时保留边缘
/// </summary>
public class MedianBlurOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MedianBlur;

    public MedianBlurOperator(ILogger<MedianBlurOperator> logger) : base(logger)
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

        var kernelSize = GetIntParam(@operator, "KernelSize", 5, min: 1, max: 31);

        // 确保核大小为奇数
        if (kernelSize % 2 == 0)
            kernelSize++;

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = MatPool.Shared.Rent(src.Width, src.Height, src.Type());
        Cv2.MedianBlur(src, dst, kernelSize);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelSize = GetIntParam(@operator, "KernelSize", 5);
        if (kernelSize < 1 || kernelSize > 31)
        {
            return ValidationResult.Invalid("核大小必须在 1-31 之间");
        }
        return ValidationResult.Valid();
    }
}
