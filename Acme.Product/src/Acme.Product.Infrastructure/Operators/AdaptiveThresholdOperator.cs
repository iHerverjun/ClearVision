using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Adaptive Threshold",
    Description = "Local mean or Gaussian adaptive thresholding.",
    Category = "Preprocessing",
    IconName = "adaptive-threshold",
    Keywords = new[] { "adaptive", "threshold", "local threshold", "mean", "gaussian" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("MaxValue", "Max Value", "double", DefaultValue = 255.0, Min = 0.0, Max = 255.0)]
[OperatorParam("AdaptiveMethod", "Adaptive Method", "enum", DefaultValue = "Gaussian", Options = new[] { "Gaussian|Gaussian", "Mean|Mean" })]
[OperatorParam("ThresholdType", "Threshold Type", "enum", DefaultValue = "Binary", Options = new[] { "Binary|Binary", "BinaryInv|Binary Inv" })]
[OperatorParam("BlockSize", "Block Size", "int", DefaultValue = 11, Min = 3, Max = 51)]
[OperatorParam("C", "C", "double", DefaultValue = 2.0, Min = -100.0, Max = 100.0)]
public class AdaptiveThresholdOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.AdaptiveThreshold;

    public AdaptiveThresholdOperator(ILogger<AdaptiveThresholdOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No input image provided."));
        }

        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0, min: 0, max: 255);
        var adaptiveMethod = GetStringParam(@operator, "AdaptiveMethod", "Gaussian");
        var thresholdType = GetStringParam(@operator, "ThresholdType", "Binary");
        var blockSize = GetIntParam(@operator, "BlockSize", 11, min: 3, max: 51);
        var c = GetDoubleParam(@operator, "C", 2.0);

        if (blockSize % 2 == 0)
        {
            blockSize++;
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        var adaptiveType = adaptiveMethod.ToLowerInvariant() switch
        {
            "mean" => AdaptiveThresholdTypes.MeanC,
            "gaussian" => AdaptiveThresholdTypes.GaussianC,
            _ => AdaptiveThresholdTypes.GaussianC
        };

        var resolvedThresholdType = thresholdType.ToLowerInvariant() switch
        {
            "binary" => ThresholdTypes.Binary,
            "binaryinv" => ThresholdTypes.BinaryInv,
            _ => ThresholdTypes.Binary
        };

        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, maxValue, adaptiveType, resolvedThresholdType, blockSize, c);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(binary.Clone(), new Dictionary<string, object>
        {
            ["AdaptiveMethod"] = adaptiveMethod,
            ["BlockSize"] = blockSize,
            ["C"] = c
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0);
        if (maxValue < 0 || maxValue > 255)
        {
            return ValidationResult.Invalid("MaxValue must be between 0 and 255.");
        }

        var blockSize = GetIntParam(@operator, "BlockSize", 11);
        if (blockSize < 3 || blockSize > 51)
        {
            return ValidationResult.Invalid("BlockSize must be between 3 and 51.");
        }

        var adaptiveMethod = GetStringParam(@operator, "AdaptiveMethod", "Gaussian").ToLowerInvariant();
        if (adaptiveMethod is not "mean" and not "gaussian")
        {
            return ValidationResult.Invalid($"Unsupported adaptive method: {adaptiveMethod}");
        }

        var thresholdType = GetStringParam(@operator, "ThresholdType", "Binary").ToLowerInvariant();
        if (thresholdType is not "binary" and not "binaryinv")
        {
            return ValidationResult.Invalid($"Unsupported threshold type: {thresholdType}");
        }

        return ValidationResult.Valid();
    }
}
