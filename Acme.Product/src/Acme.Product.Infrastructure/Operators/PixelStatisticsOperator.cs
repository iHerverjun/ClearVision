// PixelStatisticsOperator.cs
// 像素统计算子
// 统计图像像素均值、方差与分位数指标
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "像素统计",
    Description = "Computes ROI/masked pixel-level statistics.",
    Category = "检测",
    IconName = "pixel-stats",
    Keywords = new[] { "pixel statistics", "mean", "stddev", "min max", "non-zero" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Mask", "Mask", PortDataType.Image, IsRequired = false)]
[OutputPort("Mean", "Mean", PortDataType.Float)]
[OutputPort("StdDev", "StdDev", PortDataType.Float)]
[OutputPort("Min", "Min", PortDataType.Integer)]
[OutputPort("Max", "Max", PortDataType.Integer)]
[OutputPort("Median", "Median", PortDataType.Integer)]
[OutputPort("NonZeroCount", "NonZero Count", PortDataType.Integer)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("Channel", "Channel", "enum", DefaultValue = "Gray", Options = new[] { "Gray|Gray", "R|R", "G|G", "B|B", "All|All" })]
public class PixelStatisticsOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PixelStatistics;

    public PixelStatisticsOperator(ILogger<PixelStatisticsOperator> logger) : base(logger)
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

        var channel = GetStringParam(@operator, "Channel", "Gray");
        var roi = ResolveRoi(@operator, src.Width, src.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        using var roiMat = new Mat(src, roi);
        using var analysis = ExtractChannel(roiMat, channel);
        using var mask = ResolveMask(inputs, analysis.Size());

        Cv2.MeanStdDev(analysis, out var mean, out var stddev, mask);
        Cv2.MinMaxLoc(analysis, out var minValue, out var maxValue, out _, out _, mask);

        using var nzSource = new Mat();
        if (mask.Empty())
        {
            analysis.CopyTo(nzSource);
        }
        else
        {
            Cv2.BitwiseAnd(analysis, analysis, nzSource, mask);
        }

        var nonZeroCount = Cv2.CountNonZero(nzSource);
        var median = ComputeMedian(analysis, mask);

        var output = new Dictionary<string, object>
        {
            { "Mean", mean.Val0 },
            { "StdDev", stddev.Val0 },
            { "Min", minValue },
            { "Max", maxValue },
            { "Median", median },
            { "NonZeroCount", nonZeroCount }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var channel = GetStringParam(@operator, "Channel", "Gray");
        var validChannels = new[] { "Gray", "R", "G", "B", "All" };
        if (!validChannels.Contains(channel, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Channel must be Gray, R, G, B or All");
        }

        return ValidationResult.Valid();
    }

    private static Mat ExtractChannel(Mat src, string channel)
    {
        if (channel.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (src.Channels() == 1)
            {
                return src.Clone();
            }

            var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        if (channel.Equals("Gray", StringComparison.OrdinalIgnoreCase))
        {
            if (src.Channels() == 1)
            {
                return src.Clone();
            }

            var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var channels = src.Split();
        try
        {
            return channel.ToUpperInvariant() switch
            {
                "R" => channels[2].Clone(),
                "G" => channels[1].Clone(),
                "B" => channels[0].Clone(),
                _ => channels[0].Clone()
            };
        }
        finally
        {
            foreach (var c in channels)
            {
                c.Dispose();
            }
        }
    }

    private static Mat ResolveMask(Dictionary<string, object>? inputs, Size size)
    {
        if (inputs == null ||
            !ImageWrapper.TryGetFromInputs(inputs, "Mask", out var maskWrapper) ||
            maskWrapper == null)
        {
            return new Mat();
        }

        using var maskSrc = maskWrapper.GetMat();
        if (maskSrc.Empty())
        {
            return new Mat();
        }

        var grayMask = new Mat();
        if (maskSrc.Channels() == 1)
        {
            maskSrc.CopyTo(grayMask);
        }
        else
        {
            Cv2.CvtColor(maskSrc, grayMask, ColorConversionCodes.BGR2GRAY);
        }

        if (grayMask.Size() != size)
        {
            var resized = new Mat();
            Cv2.Resize(grayMask, resized, size);
            grayMask.Dispose();
            grayMask = resized;
        }

        Cv2.Threshold(grayMask, grayMask, 1, 255, ThresholdTypes.Binary);
        return grayMask;
    }

    private static int ComputeMedian(Mat analysis, Mat mask)
    {
        using var hist = new Mat();
        using var noMask = new Mat();
        Cv2.CalcHist(
            new[] { analysis },
            new[] { 0 },
            mask.Empty() ? noMask : mask,
            hist,
            1,
            new[] { 256 },
            new[] { new Rangef(0, 256) });

        var values = new float[256];
        var total = 0.0;
        for (var i = 0; i < 256; i++)
        {
            values[i] = hist.At<float>(i);
            total += values[i];
        }

        if (total <= 0)
        {
            return 0;
        }

        var acc = 0.0;
        for (var i = 0; i < 256; i++)
        {
            acc += values[i];
            if (acc >= total / 2.0)
            {
                return i;
            }
        }

        return 255;
    }

    private static Rect ResolveRoi(Operator @operator, int width, int height)
    {
        var x = ReadParam(@operator, "RoiX", 0);
        var y = ReadParam(@operator, "RoiY", 0);
        var w = ReadParam(@operator, "RoiW", width - x);
        var h = ReadParam(@operator, "RoiH", height - y);

        x = Math.Clamp(x, 0, Math.Max(0, width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, height - 1));
        w = Math.Clamp(w, 1, width - x);
        h = Math.Clamp(h, 1, height - y);
        return new Rect(x, y, w, h);
    }

    private static int ReadParam(Operator @operator, string name, int def)
    {
        var raw = @operator.Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        if (raw == null)
        {
            return def;
        }

        return int.TryParse(raw.ToString(), out var value) ? value : def;
    }
}
