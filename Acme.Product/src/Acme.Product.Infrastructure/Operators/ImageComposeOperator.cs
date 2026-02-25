using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class ImageComposeOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageCompose;

    public ImageComposeOperator(ILogger<ImageComposeOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image1", out var image1) || image1 == null ||
            !TryGetInputImage(inputs, "Image2", out var image2) || image2 == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Image1 and Image2 are required"));
        }

        var images = new List<Mat> { image1.GetMat(), image2.GetMat() };
        if (TryGetInputImage(inputs, "Image3", out var image3) && image3 != null)
        {
            images.Add(image3.GetMat());
        }

        if (TryGetInputImage(inputs, "Image4", out var image4) && image4 != null)
        {
            images.Add(image4.GetMat());
        }

        if (images.Any(m => m.Empty()))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("One or more input images are invalid"));
        }

        var mode = GetStringParam(@operator, "Mode", "Horizontal");
        var padding = GetIntParam(@operator, "Padding", 0, 0, 1000);
        var bgColor = ParseColor(GetStringParam(@operator, "BackgroundColor", "#000000"));

        Mat result = mode.ToLowerInvariant() switch
        {
            "horizontal" => ComposeHorizontal(images, padding, bgColor),
            "vertical" => ComposeVertical(images, padding, bgColor),
            "grid" => ComposeGrid(images, padding, bgColor),
            "channelmerge" => ComposeChannels(images),
            _ => throw new InvalidOperationException("Unsupported compose mode")
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "Horizontal");
        var validModes = new[] { "Horizontal", "Vertical", "Grid", "ChannelMerge" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be Horizontal, Vertical, Grid or ChannelMerge");
        }

        var padding = GetIntParam(@operator, "Padding", 0);
        if (padding < 0)
        {
            return ValidationResult.Invalid("Padding must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static Mat ComposeHorizontal(IReadOnlyList<Mat> images, int padding, Scalar bg)
    {
        var width = images.Sum(i => i.Width) + padding * (images.Count - 1);
        var height = images.Max(i => i.Height);
        var result = new Mat(height, width, MatType.CV_8UC3, bg);

        var x = 0;
        foreach (var img in images)
        {
            using var bgr = EnsureBgr(img);
            using var roi = new Mat(result, new Rect(x, 0, bgr.Width, bgr.Height));
            bgr.CopyTo(roi);
            x += img.Width + padding;
        }

        return result;
    }

    private static Mat ComposeVertical(IReadOnlyList<Mat> images, int padding, Scalar bg)
    {
        var width = images.Max(i => i.Width);
        var height = images.Sum(i => i.Height) + padding * (images.Count - 1);
        var result = new Mat(height, width, MatType.CV_8UC3, bg);

        var y = 0;
        foreach (var img in images)
        {
            using var bgr = EnsureBgr(img);
            using var roi = new Mat(result, new Rect(0, y, bgr.Width, bgr.Height));
            bgr.CopyTo(roi);
            y += img.Height + padding;
        }

        return result;
    }

    private static Mat ComposeGrid(IReadOnlyList<Mat> images, int padding, Scalar bg)
    {
        var cols = 2;
        var rows = (int)Math.Ceiling(images.Count / 2.0);
        var cellW = images.Max(i => i.Width);
        var cellH = images.Max(i => i.Height);

        var width = cols * cellW + padding * (cols - 1);
        var height = rows * cellH + padding * (rows - 1);
        var result = new Mat(height, width, MatType.CV_8UC3, bg);

        for (var i = 0; i < images.Count; i++)
        {
            var r = i / cols;
            var c = i % cols;
            var x = c * (cellW + padding);
            var y = r * (cellH + padding);
            using var bgr = EnsureBgr(images[i]);
            using var roi = new Mat(result, new Rect(x, y, bgr.Width, bgr.Height));
            bgr.CopyTo(roi);
        }

        return result;
    }

    private static Mat ComposeChannels(IReadOnlyList<Mat> images)
    {
        var channels = images.Select(EnsureGray).ToList();
        while (channels.Count < 3)
        {
            channels.Add(new Mat(channels[0].Rows, channels[0].Cols, MatType.CV_8UC1, Scalar.Black));
        }

        if (channels.Count > 4)
        {
            channels = channels.Take(4).ToList();
        }

        var merged = new Mat();
        Cv2.Merge(channels.Take(3).ToArray(), merged);

        foreach (var c in channels)
        {
            c.Dispose();
        }

        return merged;
    }

    private static Mat EnsureBgr(Mat src)
    {
        if (src.Channels() == 3)
        {
            return src.Clone();
        }

        var result = new Mat();
        Cv2.CvtColor(src, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat EnsureGray(Mat src)
    {
        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var result = new Mat();
        Cv2.CvtColor(src, result, ColorConversionCodes.BGR2GRAY);
        return result;
    }

    private static Scalar ParseColor(string value)
    {
        var text = value?.Trim() ?? "#000000";
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return Scalar.Black;
        }

        if (!byte.TryParse(text[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(text[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return Scalar.Black;
        }

        return new Scalar(b, g, r);
    }
}

