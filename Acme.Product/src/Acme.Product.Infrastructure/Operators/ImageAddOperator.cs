// ImageAddOperator.cs
// 图像加法算子 - 图像叠加// 功能实现合并
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像加法算子 - 图像叠加/合并
/// 【第三优先级】图像预处理算子扩展
/// </summary>
public class ImageAddOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageAdd;

    public ImageAddOperator(ILogger<ImageAddOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取两幅输入图像
        if (!TryGetInputImage(inputs, "Image1", out var image1Wrapper) || image1Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第一幅输入图像(Image1)"));
        }

        if (!TryGetInputImage(inputs, "Image2", out var image2Wrapper) || image2Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第二幅输入图像(Image2)"));
        }

        // 获取参数
        var scale1 = GetDoubleParam(@operator, "Scale1", 1.0, min: 0, max: 10.0);
        var scale2 = GetDoubleParam(@operator, "Scale2", 1.0, min: 0, max: 10.0);
        var offset = GetDoubleParam(@operator, "Offset", 0, min: -255, max: 255);

        var src1 = image1Wrapper.GetMat();
        var src2 = image2Wrapper.GetMat();

        if (src1.Empty() || src2.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = new Mat();

        // 确保两幅图像尺寸相同
        if (src1.Size() != src2.Size())
        {
            // 调整第二幅图像大小以匹配第一幅
            using var resized2 = new Mat();
            Cv2.Resize(src2, resized2, src1.Size());
            Cv2.AddWeighted(src1, scale1, resized2, scale2, offset, dst);
        }
        else
        {
            Cv2.AddWeighted(src1, scale1, src2, scale2, offset, dst);
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Scale1", scale1 },
            { "Scale2", scale2 },
            { "Offset", offset }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var scale1 = GetDoubleParam(@operator, "Scale1", 1.0);
        if (scale1 < 0 || scale1 > 10.0)
            return ValidationResult.Invalid("图像1缩放因子必须在 0-10.0 之间");

        var scale2 = GetDoubleParam(@operator, "Scale2", 1.0);
        if (scale2 < 0 || scale2 > 10.0)
            return ValidationResult.Invalid("图像2缩放因子必须在 0-10.0 之间");

        return ValidationResult.Valid();
    }
}
