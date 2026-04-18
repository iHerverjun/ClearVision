using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "直方图均衡化",
    Description = "Supports global histogram equalization and CLAHE.",
    Category = "预处理",
    IconName = "histogram",
    Keywords = new[] { "histogram", "equalization", "contrast", "clahe" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "输出图像", PortDataType.Image)]
[OperatorParam("Method", "方法", "enum", DefaultValue = "Global", Options = new[] { "Global|全局均衡化", "CLAHE|CLAHE自适应" })]
[OperatorParam("ClipLimit", "裁剪限制", "double", DefaultValue = 2.0, Min = 0.0, Max = 100.0)]
[OperatorParam("TileGridSize", "网格大小", "int", DefaultValue = 8, Min = 1, Max = 64)]
[OperatorParam("ApplyToEachChannel", "逐通道处理", "bool", DefaultValue = false)]
public class HistogramEqualizationOperator : OperatorBase
{
    private const int DefaultTileGridSize = 8;
    private const int MinTileGridSize = 1;
    private const int MaxTileGridSize = 64;

    public override OperatorType OperatorType => OperatorType.HistogramEqualization;

    public HistogramEqualizationOperator(ILogger<HistogramEqualizationOperator> logger) : base(logger)
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

        var method = GetStringParam(@operator, "Method", "Global");
        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0, min: 0, max: 100);
        var tileGridSize = GetTileGridSize(@operator, clampToRange: true);
        var applyToEachChannel = GetBoolParam(@operator, "ApplyToEachChannel", false);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var dst = method.Equals("CLAHE", StringComparison.OrdinalIgnoreCase)
            ? ApplyClahe(src, clipLimit, tileGridSize, applyToEachChannel)
            : ApplyGlobalEqualization(src, applyToEachChannel);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Method", method },
            { "ClipLimit", clipLimit },
            { "TileGridSize", tileGridSize },
            { "ApplyToEachChannel", applyToEachChannel },
            { "Channels", dst.Channels() }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "Global").ToLowerInvariant();
        var validMethods = new[] { "global", "clahe" };
        if (!validMethods.Contains(method))
        {
            return ValidationResult.Invalid($"Unsupported histogram equalization method: {method}");
        }

        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0);
        if (clipLimit < 0 || clipLimit > 100)
        {
            return ValidationResult.Invalid("ClipLimit must be between 0 and 100.");
        }

        var tileGridSize = GetTileGridSize(@operator, clampToRange: false);
        if (tileGridSize < MinTileGridSize || tileGridSize > MaxTileGridSize)
        {
            return ValidationResult.Invalid($"TileGridSize must be between {MinTileGridSize} and {MaxTileGridSize}.");
        }

        return ValidationResult.Valid();
    }

    private int GetTileGridSize(Operator @operator, bool clampToRange)
    {
        var tileGridSize = ResolveRawTileGridSize(@operator);
        if (!clampToRange)
        {
            return tileGridSize;
        }

        return Math.Clamp(tileGridSize, MinTileGridSize, MaxTileGridSize);
    }

    private int ResolveRawTileGridSize(Operator @operator)
    {
        var hasTileGridSize = @operator.Parameters.Any(parameter =>
            string.Equals(parameter.Name, "TileGridSize", StringComparison.OrdinalIgnoreCase));
        var hasLegacyTileSize = @operator.Parameters.Any(parameter =>
            string.Equals(parameter.Name, "TileSize", StringComparison.OrdinalIgnoreCase));

        if (hasTileGridSize)
        {
            var tileGridSize = GetIntParam(@operator, "TileGridSize", DefaultTileGridSize);
            if (hasLegacyTileSize && tileGridSize == DefaultTileGridSize)
            {
                // Old flows can carry only TileSize while a default TileGridSize is seeded from metadata.
                return GetIntParam(@operator, "TileSize", DefaultTileGridSize);
            }

            return tileGridSize;
        }

        if (hasLegacyTileSize)
        {
            return GetIntParam(@operator, "TileSize", DefaultTileGridSize);
        }

        return DefaultTileGridSize;
    }

    private static Mat ApplyClahe(Mat src, double clipLimit, int tileGridSize, bool applyToEachChannel)
    {
        using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));

        if (src.Channels() == 1)
        {
            return ApplySingleChannelByteCompatible(src, channel =>
            {
                var result = new Mat();
                clahe.Apply(channel, result);
                return result;
            });
        }

        if (applyToEachChannel)
        {
            return ApplyPerChannelByteCompatible(src, channel =>
            {
                var result = new Mat();
                clahe.Apply(channel, result);
                return result;
            });
        }

        return ApplyLumaChannelByteCompatible(src, ColorConversionCodes.BGR2Lab, ColorConversionCodes.Lab2BGR, channel =>
        {
            var result = new Mat();
            clahe.Apply(channel, result);
            return result;
        });
    }

    private static Mat ApplyGlobalEqualization(Mat src, bool applyToEachChannel)
    {
        if (src.Channels() == 1)
        {
            return ApplySingleChannelByteCompatible(src, channel =>
            {
                var result = new Mat();
                Cv2.EqualizeHist(channel, result);
                return result;
            });
        }

        if (applyToEachChannel)
        {
            return ApplyPerChannelByteCompatible(src, channel =>
            {
                var result = new Mat();
                Cv2.EqualizeHist(channel, result);
                return result;
            });
        }

        return ApplyLumaChannelByteCompatible(src, ColorConversionCodes.BGR2YUV, ColorConversionCodes.YUV2BGR, channel =>
        {
            var result = new Mat();
            Cv2.EqualizeHist(channel, result);
            return result;
        });
    }

    private static Mat ApplyPerChannelByteCompatible(Mat src, Func<Mat, Mat> processor)
    {
        Cv2.Split(src, out var channels);
        var processed = new Mat[channels.Length];

        try
        {
            for (var i = 0; i < channels.Length; i++)
            {
                processed[i] = ApplySingleChannelByteCompatible(channels[i], processor);
            }

            var result = new Mat();
            Cv2.Merge(processed, result);
            return result;
        }
        finally
        {
            foreach (var mat in channels)
            {
                mat.Dispose();
            }

            foreach (var mat in processed)
            {
                mat?.Dispose();
            }
        }
    }

    private static Mat ApplyLumaChannelByteCompatible(
        Mat src,
        ColorConversionCodes toColorSpace,
        ColorConversionCodes fromColorSpace,
        Func<Mat, Mat> processor)
    {
        using var converted = new Mat();
        Cv2.CvtColor(src, converted, toColorSpace);
        Cv2.Split(converted, out var channels);

        try
        {
            using var processedLuma = ApplySingleChannelByteCompatible(channels[0], processor);
            channels[0].Dispose();
            channels[0] = processedLuma.Clone();

            using var merged = new Mat();
            Cv2.Merge(channels, merged);

            var result = new Mat();
            Cv2.CvtColor(merged, result, fromColorSpace);
            return result;
        }
        finally
        {
            foreach (var mat in channels)
            {
                mat.Dispose();
            }
        }
    }

    private static Mat ApplySingleChannelByteCompatible(Mat src, Func<Mat, Mat> processor)
    {
        using var byteCompatible = OperatorImageDepthHelper.ConvertSingleChannelToByte(src, out _, out _);
        using var processedByte = processor(byteCompatible);
        return OperatorImageDepthHelper.RestoreByteImageToSourceDepth(processedByte, src);
    }
}
