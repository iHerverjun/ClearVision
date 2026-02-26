using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "直方图分析",
    Description = "Computes histogram and distribution statistics for selected channel.",
    Category = "检测",
    IconName = "histogram",
    Keywords = new[] { "histogram", "distribution", "peak", "median" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Mean", "Mean", PortDataType.Float)]
[OutputPort("StdDev", "StdDev", PortDataType.Float)]
[OutputPort("Mode", "Mode", PortDataType.Integer)]
[OutputPort("Median", "Median", PortDataType.Integer)]
[OutputPort("Peak", "Peak", PortDataType.Integer)]
[OutputPort("Valley", "Valley", PortDataType.Integer)]
[OperatorParam("Channel", "Channel", "enum", DefaultValue = "Gray", Options = new[] { "Gray|Gray", "R|R", "G|G", "B|B" })]
[OperatorParam("BinCount", "Bin Count", "int", DefaultValue = 256, Min = 2, Max = 1024)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0, Min = 0)]
public class HistogramAnalysisOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.HistogramAnalysis;

    public HistogramAnalysisOperator(ILogger<HistogramAnalysisOperator> logger) : base(logger)
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

        var channelName = GetStringParam(@operator, "Channel", "Gray");
        var binCount = GetIntParam(@operator, "BinCount", 256, 2, 1024);
        var roi = ResolveRoi(@operator, src.Width, src.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        using var roiMat = new Mat(src, roi);
        using var channelMat = ExtractChannel(roiMat, channelName);

        Cv2.MeanStdDev(channelMat, out var mean, out var stddev);
        using var hist = new Mat();
        Cv2.CalcHist(
            new[] { channelMat },
            new[] { 0 },
            null,
            hist,
            1,
            new[] { binCount },
            new[] { new Rangef(0, 256) });

        var values = new float[binCount];
        for (var i = 0; i < binCount; i++)
        {
            values[i] = hist.At<float>(i);
        }

        var total = values.Sum(v => (double)v);
        var mode = ArgMax(values);
        var peak = mode;
        var valley = ArgMin(values);
        var median = ComputeMedianBin(values, total);

        var chart = DrawHistogram(values, 512, 220);
        var output = new Dictionary<string, object>
        {
            { "Mean", mean.Val0 },
            { "StdDev", stddev.Val0 },
            { "Mode", mode },
            { "Median", median },
            { "Peak", peak },
            { "Valley", valley }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(chart, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var channel = GetStringParam(@operator, "Channel", "Gray");
        var validChannels = new[] { "Gray", "R", "G", "B" };
        if (!validChannels.Contains(channel, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Channel must be Gray, R, G or B");
        }

        var binCount = GetIntParam(@operator, "BinCount", 256);
        if (binCount < 2)
        {
            return ValidationResult.Invalid("BinCount must be >= 2");
        }

        return ValidationResult.Valid();
    }

    private static Mat ExtractChannel(Mat src, string channelName)
    {
        if (channelName.Equals("Gray", StringComparison.OrdinalIgnoreCase))
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
            return channelName.ToUpperInvariant() switch
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

    private static Rect ResolveRoi(Operator @operator, int width, int height)
    {
        var x = GetClampedInt(@operator, "RoiX", 0, 0, Math.Max(0, width - 1));
        var y = GetClampedInt(@operator, "RoiY", 0, 0, Math.Max(0, height - 1));
        var w = GetClampedInt(@operator, "RoiW", width - x, 1, width);
        var h = GetClampedInt(@operator, "RoiH", height - y, 1, height);
        w = Math.Min(w, width - x);
        h = Math.Min(h, height - y);
        return new Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    private static int GetClampedInt(Operator @operator, string name, int def, int min, int max)
    {
        var value = @operator.Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        if (value == null)
        {
            return def;
        }

        if (!int.TryParse(value.ToString(), out var parsed))
        {
            parsed = def;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static int ArgMax(IReadOnlyList<float> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var idx = 0;
        var best = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] > best)
            {
                best = values[i];
                idx = i;
            }
        }

        return idx;
    }

    private static int ArgMin(IReadOnlyList<float> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var idx = 0;
        var best = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] < best)
            {
                best = values[i];
                idx = i;
            }
        }

        return idx;
    }

    private static int ComputeMedianBin(IReadOnlyList<float> values, double total)
    {
        if (values.Count == 0 || total <= 0)
        {
            return 0;
        }

        var acc = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            acc += values[i];
            if (acc >= total / 2.0)
            {
                return i;
            }
        }

        return values.Count - 1;
    }

    private static Mat DrawHistogram(IReadOnlyList<float> values, int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        if (values.Count == 0)
        {
            return image;
        }

        var max = Math.Max(1e-6f, values.Max());
        var binWidth = width / (double)values.Count;

        for (var i = 1; i < values.Count; i++)
        {
            var x1 = (int)Math.Round((i - 1) * binWidth);
            var y1 = height - (int)Math.Round(values[i - 1] / max * (height - 10));
            var x2 = (int)Math.Round(i * binWidth);
            var y2 = height - (int)Math.Round(values[i] / max * (height - 10));
            Cv2.Line(image, new Point(x1, y1), new Point(x2, y2), new Scalar(0, 255, 255), 1);
        }

        return image;
    }
}

