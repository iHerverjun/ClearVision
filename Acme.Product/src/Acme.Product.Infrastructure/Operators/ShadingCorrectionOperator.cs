// ShadingCorrectionOperator.cs
// 光照校正算子
// 对不均匀光照造成的阴影进行补偿校正
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "光照校正",
    Description = "Corrects uneven illumination by background or model-based methods.",
    Category = "预处理",
    IconName = "shading",
    Keywords = new[] { "shading", "flat field", "illumination", "background" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Background", "Background", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "GaussianModel", Options = new[] { "DivideByBackground|DivideByBackground", "GaussianModel|GaussianModel", "MorphologicalTopHat|MorphologicalTopHat" })]
[OperatorParam("KernelSize", "Kernel Size", "int", DefaultValue = 51, Min = 3, Max = 501)]
[OperatorParam("ColorMode", "Color Mode", "enum", DefaultValue = "LumaOnly", Options = new[] { "LumaOnly|LumaOnly", "PerChannel|PerChannel" })]
public class ShadingCorrectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ShadingCorrection;

    public ShadingCorrectionOperator(ILogger<ShadingCorrectionOperator> logger) : base(logger)
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

        var method = GetStringParam(@operator, "Method", "GaussianModel");
        var kernelSize = ToOdd(GetIntParam(@operator, "KernelSize", 51, 3, 501));
        var colorMode = GetStringParam(@operator, "ColorMode", "LumaOnly");

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        if (src.Channels() != 1 && src.Channels() != 3)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Only 1-channel and 3-channel images are supported"));
        }

        var requiresBackground = method.Equals("DivideByBackground", StringComparison.OrdinalIgnoreCase);
        Mat? background = null;
        if (requiresBackground)
        {
            if (!TryGetInputImage(inputs, "Background", out var backgroundWrapper) || backgroundWrapper == null)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Background input is required for DivideByBackground mode"));
            }

            background = backgroundWrapper.GetMat();
            if (background.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Background image is invalid"));
            }
        }

        Mat result;
        if (src.Channels() == 1)
        {
            result = CorrectSingleChannel(src, method, kernelSize, background);
        }
        else if (colorMode.Equals("PerChannel", StringComparison.OrdinalIgnoreCase))
        {
            result = ApplyPerChannel(src, background, (channel, backgroundChannel) =>
                CorrectSingleChannel(channel, method, kernelSize, backgroundChannel));
        }
        else if (colorMode.Equals("LumaOnly", StringComparison.OrdinalIgnoreCase))
        {
            result = ApplyLumaChannel(src, background, (channel, backgroundChannel) =>
                CorrectSingleChannel(channel, method, kernelSize, backgroundChannel));
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Unsupported color mode"));
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
        var method = GetStringParam(@operator, "Method", "GaussianModel");
        var validMethods = new[] { "DivideByBackground", "GaussianModel", "MorphologicalTopHat" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be DivideByBackground, GaussianModel or MorphologicalTopHat");
        }

        var kernel = GetIntParam(@operator, "KernelSize", 51);
        if (kernel < 3)
        {
            return ValidationResult.Invalid("KernelSize must be >= 3");
        }

        var colorMode = GetStringParam(@operator, "ColorMode", "LumaOnly");
        var validColorModes = new[] { "LumaOnly", "PerChannel" };
        if (!validColorModes.Contains(colorMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ColorMode must be LumaOnly or PerChannel");
        }

        return ValidationResult.Valid();
    }

    private static Mat CorrectSingleChannel(Mat src, string method, int kernelSize, Mat? background)
    {
        return method.ToLowerInvariant() switch
        {
            "dividebybackground" when background != null => CorrectByBackground(src, background),
            "dividebybackground" => throw new InvalidOperationException("Background input is required for DivideByBackground mode"),
            "gaussianmodel" => CorrectByGaussianModel(src, kernelSize),
            "morphologicaltophat" => CorrectByTopHat(src, kernelSize),
            _ => throw new InvalidOperationException("Unsupported shading correction method")
        };
    }

    private static Mat CorrectByBackground(Mat gray, Mat background)
    {
        using var backgroundChannel = background.Channels() == 1 ? background.Clone() : ExtractGray(background);

        using var resizedBg = new Mat();
        if (backgroundChannel.Size() != gray.Size())
        {
            Cv2.Resize(backgroundChannel, resizedBg, gray.Size());
        }
        else
        {
            backgroundChannel.CopyTo(resizedBg);
        }

        using var src32 = new Mat();
        using var bg32 = new Mat();
        gray.ConvertTo(src32, MatType.CV_32FC1);
        resizedBg.ConvertTo(bg32, MatType.CV_32FC1);

        using var eps = new Mat(bg32.Size(), bg32.Type(), new Scalar(1.0));
        using var denom = new Mat();
        Cv2.Add(bg32, eps, denom);

        using var corrected32 = new Mat();
        var targetLevel = Math.Max(1.0, Cv2.Mean(bg32).Val0);
        Cv2.Divide(src32, denom, corrected32, targetLevel);
        return ConvertBackToSourceDepth(corrected32, gray);
    }

    private static Mat ApplyPerChannel(Mat src, Mat? background, Func<Mat, Mat?, Mat> processor)
    {
        Cv2.Split(src, out var srcChannels);
        Mat[]? backgroundChannels = null;
        Mat? sharedBackground = null;
        var processed = new Mat[srcChannels.Length];

        try
        {
            if (background != null)
            {
                if (background.Channels() == src.Channels())
                {
                    Cv2.Split(background, out backgroundChannels);
                }
                else if (background.Channels() == 1)
                {
                    sharedBackground = background.Clone();
                }
                else
                {
                    throw new InvalidOperationException("Background must be grayscale or match the input channel count for PerChannel mode");
                }
            }

            for (var i = 0; i < srcChannels.Length; i++)
            {
                var backgroundChannel = backgroundChannels != null ? backgroundChannels[i] : sharedBackground;
                processed[i] = processor(srcChannels[i], backgroundChannel);
            }

            var result = new Mat();
            Cv2.Merge(processed, result);
            return result;
        }
        finally
        {
            foreach (var channel in srcChannels)
            {
                channel.Dispose();
            }

            if (backgroundChannels != null)
            {
                foreach (var channel in backgroundChannels)
                {
                    channel.Dispose();
                }
            }

            sharedBackground?.Dispose();

            foreach (var channel in processed)
            {
                channel?.Dispose();
            }
        }
    }

    private static Mat ApplyLumaChannel(Mat src, Mat? background, Func<Mat, Mat?, Mat> processor)
    {
        return ApplyLumaChannel(src, background, processor, allowByteFallback: true);
    }

    private static Mat ApplyLumaChannel(Mat src, Mat? background, Func<Mat, Mat?, Mat> processor, bool allowByteFallback)
    {
        using var yuv = new Mat();
        Cv2.CvtColor(src, yuv, ColorConversionCodes.BGR2YUV);
        Cv2.Split(yuv, out var channels);
        Mat? backgroundLuma = background == null ? null : ExtractGray(background);

        try
        {
            using var processedLuma = processor(channels[0], backgroundLuma);
            if (processedLuma.Type() != channels[0].Type())
            {
                if (!allowByteFallback || processedLuma.Depth() != MatType.CV_8U)
                {
                    throw new InvalidOperationException("Luma-only shading correction requires matching channel depths before merge.");
                }

                // When luma processing collapses to 8-bit, re-run on an 8-bit color view so Y/U/V stay in the same contract.
                using var byteSrc = ConvertToByteCompatibleImage(src);
                Mat? byteBackground = null;

                try
                {
                    if (background != null)
                    {
                        byteBackground = ConvertToByteCompatibleImage(background);
                    }

                    return ApplyLumaChannel(byteSrc, byteBackground, processor, allowByteFallback: false);
                }
                finally
                {
                    byteBackground?.Dispose();
                }
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
            backgroundLuma?.Dispose();

            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat ExtractGray(Mat src)
    {
        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
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
            var minValue = double.PositiveInfinity;
            var maxValue = double.NegativeInfinity;

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

    private static Mat CorrectByGaussianModel(Mat gray, int kernelSize)
    {
        using var background = new Mat();
        Cv2.GaussianBlur(gray, background, new Size(kernelSize, kernelSize), 0);

        using var src32 = new Mat();
        using var bg32 = new Mat();
        gray.ConvertTo(src32, MatType.CV_32FC1);
        background.ConvertTo(bg32, MatType.CV_32FC1);

        using var eps = new Mat(bg32.Size(), bg32.Type(), new Scalar(1.0));
        using var denom = new Mat();
        Cv2.Add(bg32, eps, denom);

        using var corrected32 = new Mat();
        var targetLevel = Math.Max(1.0, Cv2.Mean(bg32).Val0);
        Cv2.Divide(src32, denom, corrected32, targetLevel);
        return ConvertBackToSourceDepth(corrected32, gray);
    }

    private static Mat CorrectByTopHat(Mat gray, int kernelSize)
    {
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        var result = new Mat();
        Cv2.MorphologyEx(gray, result, MorphTypes.TopHat, kernel);
        return result;
    }

    private static int ToOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }

    private static Mat ConvertBackToSourceDepth(Mat corrected32, Mat reference)
    {
        if (reference.Depth() == MatType.CV_32F)
        {
            return corrected32.Clone();
        }

        if (reference.Depth() == MatType.CV_64F)
        {
            var result64 = new Mat();
            corrected32.ConvertTo(result64, MatType.CV_64FC1);
            return result64;
        }

        var (minValue, maxValue) = reference.Depth() switch
        {
            MatType.CV_8U => (0.0, 255.0),
            MatType.CV_16U => (0.0, (double)ushort.MaxValue),
            _ => (0.0, 255.0)
        };

        using var clipped = new Mat();
        Cv2.Min(corrected32, new Scalar(maxValue), clipped);
        Cv2.Max(clipped, new Scalar(minValue), clipped);

        var result = new Mat();
        clipped.ConvertTo(result, MatType.MakeType(reference.Depth(), 1));
        return result;
    }
}

