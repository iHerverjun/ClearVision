// ImageDiffOperator.cs
// 图像对比算子实现
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像对比算子 - 差分分析
/// </summary>
public class ImageDiffOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageDiff;

    public ImageDiffOperator(ILogger<ImageDiffOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "BaseImage", out var imgA) || imgA == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("基准图像不能为空"));

        if (!TryGetInputImage(inputs, "CompareImage", out var imgB) || imgB == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("对比图像不能为空"));

        using var matA = imgA.GetMat();
        using var matB = imgB.GetMat();

        if (matA.Size() != matB.Size())
            return Task.FromResult(OperatorExecutionOutput.Failure("算子输入图像尺寸不一致"));

        using var diff = new Mat();
        Cv2.Absdiff(matA, matB, diff);

        using var grayDiff = diff.Channels() > 1 ? diff.CvtColor(ColorConversionCodes.BGR2GRAY) : diff.Clone();
        double diffRate = (double)Cv2.CountNonZero(grayDiff) / (matA.Width * matA.Height);

        var output = CreateImageOutput(diff.Clone());
        output["DiffRate"] = diffRate;

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }
}
