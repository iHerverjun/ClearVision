using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Applies a mean (box blur) filter to the input image.
/// </summary>
[OperatorMeta(
    DisplayName = "均值滤波",
    Description = "Applies mean (box blur) filtering to smooth image noise.",
    Category = "预处理",
    IconName = "filter",
    Keywords = new[] { "mean filter", "box blur", "box filter", "smooth", "denoise" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("KernelSize", "Kernel Size", "int", DefaultValue = 5, Min = 1, Max = 63)]
[OperatorParam(
    "BorderType",
    "Border Type",
    "enum",
    DefaultValue = "4",
    Options = new[] { "0|Constant", "1|Replicate", "2|Reflect", "3|Wrap", "4|Default" }
)]
public class MeanFilterOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MeanFilter;

    public MeanFilterOperator(ILogger<MeanFilterOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No input image provided"));
        }

        var kernelSize = GetIntParam(@operator, "KernelSize", 5, min: 1, max: 63);
        var borderType = GetIntParam(@operator, "BorderType", 4, min: 0, max: 7);

        // Force odd kernel size to keep a symmetric anchor at center.
        if (kernelSize % 2 == 0)
        {
            kernelSize++;
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var dst = new Mat();
        Cv2.Blur(src, dst, new Size(kernelSize, kernelSize), new Point(-1, -1), (BorderTypes)borderType);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelSize = GetIntParam(@operator, "KernelSize", 5);
        if (kernelSize < 1 || kernelSize > 63)
        {
            return ValidationResult.Invalid("KernelSize must be in [1, 63]");
        }

        return ValidationResult.Valid();
    }
}
