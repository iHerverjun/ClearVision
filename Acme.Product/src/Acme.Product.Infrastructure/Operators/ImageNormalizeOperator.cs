using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "图像归一化",
    Description = "Normalizes pixel distribution for robust downstream processing.",
    Category = "预处理",
    IconName = "normalize",
    Keywords = new[] { "normalize", "minmax", "zscore", "equalize" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "MinMax", Options = new[] { "MinMax|MinMax", "ZScore|ZScore", "Histogram|Histogram" })]
[OperatorParam("Alpha", "Alpha", "double", DefaultValue = 0.0)]
[OperatorParam("Beta", "Beta", "double", DefaultValue = 255.0)]
public class ImageNormalizeOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageNormalize;

    public ImageNormalizeOperator(ILogger<ImageNormalizeOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var method = GetStringParam(@operator, "Method", "MinMax");
        var alpha = GetDoubleParam(@operator, "Alpha", 0.0, -10000.0, 10000.0);
        var beta = GetDoubleParam(@operator, "Beta", 255.0, -10000.0, 10000.0);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        Mat normalized;
        switch (method.ToLowerInvariant())
        {
            case "minmax":
            {
                normalized = new Mat();
                Cv2.Normalize(gray, normalized, alpha, beta, NormTypes.MinMax, MatType.CV_8UC1);
                break;
            }
            case "zscore":
            {
                Cv2.MeanStdDev(gray, out var mean, out var stddev);
                var sigma = Math.Max(1e-6, stddev.Val0);
                using var src32 = new Mat();
                gray.ConvertTo(src32, MatType.CV_32FC1);

                using var centered = new Mat();
                Cv2.Subtract(src32, new Scalar(mean.Val0), centered);
                using var z = new Mat();
                Cv2.Divide(centered, new Scalar(sigma), z);

                normalized = new Mat();
                Cv2.Normalize(z, normalized, alpha, beta, NormTypes.MinMax, MatType.CV_8UC1);
                break;
            }
            case "histogram":
            {
                normalized = new Mat();
                Cv2.EqualizeHist(gray, normalized);
                break;
            }
            default:
                throw new InvalidOperationException("Unsupported normalize method");
        }

        if (src.Channels() == 1)
        {
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(normalized)));
        }

        var result = new Mat();
        Cv2.CvtColor(normalized, result, ColorConversionCodes.GRAY2BGR);
        normalized.Dispose();
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "MinMax");
        var validMethods = new[] { "MinMax", "ZScore", "Histogram" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be MinMax, ZScore or Histogram");
        }

        return ValidationResult.Valid();
    }
}

