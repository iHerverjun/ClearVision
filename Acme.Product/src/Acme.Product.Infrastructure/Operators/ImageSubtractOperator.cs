using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Image Subtract",
    Description = "Computes subtraction or absolute difference between two images.",
    Category = "Preprocessing",
    IconName = "subtract"
)]
[InputPort("Image1", "Image1", PortDataType.Image, IsRequired = true)]
[InputPort("Image2", "Image2", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Difference Image", PortDataType.Image)]
[OutputPort("MinDifference", "Min Difference", PortDataType.Float)]
[OutputPort("MaxDifference", "Max Difference", PortDataType.Float)]
[OutputPort("MeanDifference", "Mean Difference", PortDataType.Float)]
[OperatorParam("AbsoluteDiff", "Absolute Diff", "bool", Description = "Use absolute difference instead of signed subtraction.", DefaultValue = true)]
public class ImageSubtractOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageSubtract;

    public ImageSubtractOperator(ILogger<ImageSubtractOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image1", out var image1Wrapper) || image1Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Image1 is required."));
        }

        if (!TryGetInputImage(inputs, "Image2", out var image2Wrapper) || image2Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Image2 is required."));
        }

        var absoluteDiff = GetBoolParam(@operator, "AbsoluteDiff", true);

        var src1 = image1Wrapper.GetMat();
        var src2 = image2Wrapper.GetMat();
        if (src1.Empty() || src2.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var dst = new Mat();
        if (src1.Size() != src2.Size())
        {
            using var resized2 = new Mat();
            Cv2.Resize(src2, resized2, src1.Size());
            ApplySubtract(src1, resized2, dst, absoluteDiff);
        }
        else
        {
            ApplySubtract(src1, src2, dst, absoluteDiff);
        }

        using var statsSource = new Mat();
        if (dst.Channels() > 1)
        {
            Cv2.CvtColor(dst, statsSource, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            dst.CopyTo(statsSource);
        }

        Cv2.MinMaxLoc(statsSource, out double minVal, out double maxVal);
        var mean = Cv2.Mean(statsSource).Val0;

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "AbsoluteDiff", absoluteDiff },
            { "MinDifference", minVal },
            { "MaxDifference", maxVal },
            { "MeanDifference", mean }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }

    private static void ApplySubtract(Mat src1, Mat src2, Mat dst, bool absoluteDiff)
    {
        if (absoluteDiff)
        {
            Cv2.Absdiff(src1, src2, dst);
        }
        else
        {
            Cv2.Subtract(src1, src2, dst);
        }
    }
}
