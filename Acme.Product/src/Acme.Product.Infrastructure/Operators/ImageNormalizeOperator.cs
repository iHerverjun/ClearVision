// ImageNormalizeOperator.cs
// 图像归一化算子
// 对图像像素进行范围或分布归一化处理
// 作者：蘅芜君
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
[OperatorParam("ColorMode", "Color Mode", "enum", DefaultValue = "LumaOnly", Options = new[] { "LumaOnly|LumaOnly", "PerChannel|PerChannel" })]
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
        var colorMode = GetStringParam(@operator, "ColorMode", "LumaOnly");

        Mat result;
        if (src.Channels() == 1)
        {
            result = NormalizeSingleChannel(src, method, alpha, beta);
        }
        else if (src.Channels() == 3)
        {
            result = colorMode.Equals("PerChannel", StringComparison.OrdinalIgnoreCase)
                ? ApplyPerChannel(src, channel => NormalizeSingleChannel(channel, method, alpha, beta))
                : colorMode.Equals("LumaOnly", StringComparison.OrdinalIgnoreCase)
                    ? ApplyLumaChannel(src, channel => NormalizeSingleChannel(channel, method, alpha, beta))
                    : throw new InvalidOperationException("Unsupported color mode");
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Only 1-channel and 3-channel images are supported"));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, new Dictionary<string, object>
        {
            { "Method", method },
            { "ColorMode", src.Channels() == 1 ? "Gray" : colorMode },
            { "Channels", result.Channels() }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "MinMax");
        var validMethods = new[] { "MinMax", "ZScore", "Histogram" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be MinMax, ZScore or Histogram");
        }

        var colorMode = GetStringParam(@operator, "ColorMode", "LumaOnly");
        var validColorModes = new[] { "LumaOnly", "PerChannel" };
        if (!validColorModes.Contains(colorMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ColorMode must be LumaOnly or PerChannel");
        }

        return ValidationResult.Valid();
    }

    private static Mat NormalizeSingleChannel(Mat src, string method, double alpha, double beta)
    {
        return method.ToLowerInvariant() switch
        {
            "minmax" => NormalizeMinMax(src, alpha, beta),
            "zscore" => NormalizeZScore(src, alpha, beta),
            "histogram" => NormalizeHistogram(src),
            _ => throw new InvalidOperationException("Unsupported normalize method")
        };
    }

    private static Mat NormalizeMinMax(Mat src, double alpha, double beta)
    {
        var normalized = new Mat();
        Cv2.Normalize(src, normalized, alpha, beta, NormTypes.MinMax, MatType.CV_8UC1);
        return normalized;
    }

    private static Mat NormalizeZScore(Mat src, double alpha, double beta)
    {
        Cv2.MeanStdDev(src, out var mean, out var stddev);
        var sigma = Math.Max(1e-6, stddev.Val0);

        using var src32 = new Mat();
        src.ConvertTo(src32, MatType.CV_32FC1);

        using var centered = new Mat();
        Cv2.Subtract(src32, new Scalar(mean.Val0), centered);
        using var z = new Mat();
        Cv2.Divide(centered, new Scalar(sigma), z);

        var normalized = new Mat();
        Cv2.Normalize(z, normalized, alpha, beta, NormTypes.MinMax, MatType.CV_8UC1);
        return normalized;
    }

    private static Mat NormalizeHistogram(Mat src)
    {
        using var byteChannel = ConvertToByteChannel(src);
        var normalized = new Mat();
        Cv2.EqualizeHist(byteChannel, normalized);
        return normalized;
    }

    private static Mat ConvertToByteChannel(Mat src)
    {
        if (src.Depth() == MatType.CV_8U)
        {
            return src.Clone();
        }

        var normalized = new Mat();
        Cv2.Normalize(src, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8UC1);
        return normalized;
    }

    private static Mat ApplyPerChannel(Mat src, Func<Mat, Mat> processor)
    {
        Cv2.Split(src, out var channels);
        var processed = new Mat[channels.Length];

        try
        {
            for (var i = 0; i < channels.Length; i++)
            {
                processed[i] = processor(channels[i]);
            }

            var result = new Mat();
            Cv2.Merge(processed, result);
            return result;
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }

            foreach (var channel in processed)
            {
                channel?.Dispose();
            }
        }
    }

    private static Mat ApplyLumaChannel(Mat src, Func<Mat, Mat> processor)
    {
        return ApplyLumaChannel(src, processor, allowByteFallback: true);
    }

    private static Mat ApplyLumaChannel(Mat src, Func<Mat, Mat> processor, bool allowByteFallback)
    {
        using var yuv = new Mat();
        Cv2.CvtColor(src, yuv, ColorConversionCodes.BGR2YUV);
        Cv2.Split(yuv, out var channels);

        try
        {
            using var processedLuma = processor(channels[0]);
            if (processedLuma.Type() != channels[0].Type())
            {
                if (!allowByteFallback || processedLuma.Depth() != MatType.CV_8U)
                {
                    throw new InvalidOperationException("Luma-only normalization requires matching channel depths before merge.");
                }

                // When luma normalization collapses to 8-bit, re-run on an 8-bit color view so Y/U/V stay merge-compatible.
                using var byteSrc = ConvertToByteCompatibleImage(src);
                return ApplyLumaChannel(byteSrc, processor, allowByteFallback: false);
            }

            channels[0].Dispose();
            channels[0] = processedLuma.Clone();

            using var merged = new Mat();
            Cv2.Merge(channels, merged);

            var result = new Mat();
            Cv2.CvtColor(merged, result, ColorConversionCodes.YUV2BGR);
            return result;
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat ConvertToByteCompatibleImage(Mat src)
    {
        if (src.Depth() == MatType.CV_8U)
        {
            return src.Clone();
        }

        var converted = new Mat();
        var targetType = MatType.MakeType(MatType.CV_8U, src.Channels());

        switch (src.Depth())
        {
            case MatType.CV_16U:
                src.ConvertTo(converted, targetType, 1.0 / 256.0);
                break;
            case MatType.CV_32F:
            case MatType.CV_64F:
                var (floatMin, floatMax) = GetGlobalMinMax(src);
                if (floatMin >= 0d && floatMax <= 1d)
                {
                    src.ConvertTo(converted, targetType, 255.0);
                }
                else if (floatMin >= 0d && floatMax <= 255d)
                {
                    src.ConvertTo(converted, targetType);
                }
                else
                {
                    ConvertToByteCompatibleImageWithRangeNormalization(src, converted, targetType, floatMin, floatMax);
                }

                break;
            default:
                var (minValue, maxValue) = GetGlobalMinMax(src);
                ConvertToByteCompatibleImageWithRangeNormalization(src, converted, targetType, minValue, maxValue);
                break;
        }

        return converted;
    }

    private static void ConvertToByteCompatibleImageWithRangeNormalization(Mat src, Mat dst, MatType targetType, double minValue, double maxValue)
    {
        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            throw new InvalidOperationException("Input image contains non-finite values and cannot be converted to 8-bit color.");
        }

        if (maxValue <= minValue)
        {
            src.ConvertTo(dst, targetType, 0.0, 0.0);
            return;
        }

        var scale = 255.0 / (maxValue - minValue);
        var shift = -minValue * scale;
        src.ConvertTo(dst, targetType, scale, shift);
    }

    private static (double Min, double Max) GetGlobalMinMax(Mat src)
    {
        if (src.Channels() == 1)
        {
            double minValue;
            double maxValue;
            Cv2.MinMaxLoc(src, out minValue, out maxValue);
            return (minValue, maxValue);
        }

        Cv2.Split(src, out var channels);
        try
        {
            double minValue = double.PositiveInfinity;
            double maxValue = double.NegativeInfinity;

            foreach (var channel in channels)
            {
                double channelMin;
                double channelMax;
                Cv2.MinMaxLoc(channel, out channelMin, out channelMax);
                minValue = Math.Min(minValue, channelMin);
                maxValue = Math.Max(maxValue, channelMax);
            }

            return (minValue, maxValue);
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }
}
