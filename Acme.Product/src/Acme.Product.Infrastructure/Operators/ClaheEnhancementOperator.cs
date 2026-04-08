using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "CLAHE增强",
    Description = "Adaptive histogram equalization for local contrast enhancement.",
    Category = "预处理",
    IconName = "clahe"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "增强图像", PortDataType.Image)]
[OperatorParam("ClipLimit", "裁剪限制", "double", Description = "Limits local contrast amplification to avoid excessive noise boosting.", DefaultValue = 2.0, Min = 0, Max = 40)]
[OperatorParam("TileWidth", "网格宽度", "int", DefaultValue = 8, Min = 2, Max = 64)]
[OperatorParam("TileHeight", "网格高度", "int", DefaultValue = 8, Min = 2, Max = 64)]
[OperatorParam("ColorSpace", "颜色空间", "enum", DefaultValue = "Lab", Options = new[] { "Lab|Lab - L通道", "HSV|HSV - V通道", "Gray|灰度", "All|所有通道" })]
[OperatorParam("Channel", "目标通道", "enum", DefaultValue = "Auto", Options = new[] { "Auto|自动", "L|L通道", "V|V通道", "Y|Y通道", "All|所有通道" }, Description = "Auto follows ColorSpace. L/V/Y/All explicitly choose the processing branch.")]
public class ClaheEnhancementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ClaheEnhancement;

    public ClaheEnhancementOperator(ILogger<ClaheEnhancementOperator> logger) : base(logger)
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

        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0, min: 0, max: 40);
        var tileWidth = GetIntParam(@operator, "TileWidth", 8, min: 2, max: 64);
        var tileHeight = GetIntParam(@operator, "TileHeight", 8, min: 2, max: 64);
        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab");
        var channel = GetStringParam(@operator, "Channel", "Auto");

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileWidth, tileHeight));
        var resolution = ResolveEnhancementMode(src, colorSpace, channel);
        var dst = resolution.Mode switch
        {
            "Gray" => ApplyClaheToGray(src, clahe),
            "All" => ApplyClaheToAllChannels(src, clahe),
            "Lab" => ApplyClaheToSingleChannel(src, clahe, ColorConversionCodes.BGR2Lab, ColorConversionCodes.Lab2BGR, 0),
            "HSV" => ApplyClaheToSingleChannel(src, clahe, ColorConversionCodes.BGR2HSV, ColorConversionCodes.HSV2BGR, 2),
            "YCrCb" => ApplyClaheToSingleChannel(src, clahe, ColorConversionCodes.BGR2YCrCb, ColorConversionCodes.YCrCb2BGR, 0),
            _ => throw new InvalidOperationException($"Unsupported CLAHE mode: {resolution.Mode}")
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "ClipLimit", clipLimit },
            { "TileSize", $"{tileWidth}x{tileHeight}" },
            { "ColorSpace", colorSpace },
            { "Channel", channel },
            { "ResolvedColorSpace", resolution.ResolvedColorSpace },
            { "ResolvedChannel", resolution.ResolvedChannel }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0);
        if (clipLimit < 0 || clipLimit > 40)
        {
            return ValidationResult.Invalid("ClipLimit must be between 0 and 40.");
        }

        var tileWidth = GetIntParam(@operator, "TileWidth", 8);
        if (tileWidth < 2 || tileWidth > 64)
        {
            return ValidationResult.Invalid("TileWidth must be between 2 and 64.");
        }

        var tileHeight = GetIntParam(@operator, "TileHeight", 8);
        if (tileHeight < 2 || tileHeight > 64)
        {
            return ValidationResult.Invalid("TileHeight must be between 2 and 64.");
        }

        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab").ToLowerInvariant();
        var validSpaces = new[] { "lab", "hsv", "gray", "all" };
        if (!validSpaces.Contains(colorSpace))
        {
            return ValidationResult.Invalid($"Unsupported color space: {colorSpace}");
        }

        var channel = GetStringParam(@operator, "Channel", "Auto").ToLowerInvariant();
        var validChannels = new[] { "auto", "l", "v", "y", "all" };
        if (!validChannels.Contains(channel))
        {
            return ValidationResult.Invalid($"Unsupported channel: {channel}");
        }

        return ValidationResult.Valid();
    }

    private static (string Mode, string ResolvedColorSpace, string ResolvedChannel) ResolveEnhancementMode(
        Mat src,
        string colorSpace,
        string channel)
    {
        if (src.Channels() == 1)
        {
            return ("Gray", "Gray", "Gray");
        }

        if (!channel.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return channel.ToLowerInvariant() switch
            {
                "l" => ("Lab", "Lab", "L"),
                "v" => ("HSV", "HSV", "V"),
                "y" => ("YCrCb", "YCrCb", "Y"),
                "all" => ("All", "BGR", "All"),
                _ => throw new InvalidOperationException($"Unsupported channel: {channel}")
            };
        }

        return colorSpace.ToLowerInvariant() switch
        {
            "lab" => ("Lab", "Lab", "L"),
            "hsv" => ("HSV", "HSV", "V"),
            "gray" => ("Gray", "Gray", "Gray"),
            "all" => ("All", "BGR", "All"),
            _ => throw new InvalidOperationException($"Unsupported color space: {colorSpace}")
        };
    }

    private static Mat ApplyClaheToGray(Mat src, CLAHE clahe)
    {
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var result = new Mat();
        clahe.Apply(gray, result);
        return result;
    }

    private static Mat ApplyClaheToAllChannels(Mat src, CLAHE clahe)
    {
        if (src.Channels() == 1)
        {
            return ApplyClaheToGray(src, clahe);
        }

        Cv2.Split(src, out var channels);
        var processed = new Mat[channels.Length];

        try
        {
            for (var i = 0; i < channels.Length; i++)
            {
                processed[i] = new Mat();
                clahe.Apply(channels[i], processed[i]);
            }

            var merged = new Mat();
            Cv2.Merge(processed, merged);
            return merged;
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

    private static Mat ApplyClaheToSingleChannel(
        Mat src,
        CLAHE clahe,
        ColorConversionCodes toColorSpace,
        ColorConversionCodes fromColorSpace,
        int channelIndex)
    {
        using var converted = new Mat();
        Cv2.CvtColor(src, converted, toColorSpace);
        Cv2.Split(converted, out var channels);

        try
        {
            using var enhancedChannel = new Mat();
            clahe.Apply(channels[channelIndex], enhancedChannel);
            channels[channelIndex].Dispose();
            channels[channelIndex] = enhancedChannel.Clone();

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
}
