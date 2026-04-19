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
        var roi = MeasurementRoiHelper.ResolveRoi(@operator, src.Width, src.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        using var roiMat = new Mat(src, roi);
        using var mask = ResolveMask(inputs, roi, src.Size(), out var maskError);
        if (maskError != null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(maskError));
        }

        var analysisChannels = ResolveAnalysisChannels(roiMat, channel);
        try
        {
            var perChannelStats = new Dictionary<string, StatisticsSummary>(StringComparer.OrdinalIgnoreCase);
            var aggregateValues = analysisChannels.Count > 1 ? new List<double>() : null;

            foreach (var analysisChannel in analysisChannels)
            {
                var values = ExtractValues(analysisChannel.Data, mask);
                var stats = ComputeStatistics(values);
                perChannelStats[analysisChannel.Name] = stats;

                if (aggregateValues != null)
                {
                    aggregateValues.AddRange(values);
                }
            }

            var aggregateStats = aggregateValues == null
                ? perChannelStats[analysisChannels[0].Name]
                : ComputeStatistics(aggregateValues);

            var output = CreateStatisticsDictionary(aggregateStats);
            output["SelectedChannel"] = channel;
            output["ChannelsAnalyzed"] = analysisChannels.Select(item => item.Name).ToArray();
            output["AggregationMode"] = aggregateValues == null ? "SingleChannel" : "FlattenedChannels";

            if (analysisChannels.Count > 1)
            {
                output["ChannelStats"] = perChannelStats.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)CreateStatisticsDictionary(kvp.Value),
                    StringComparer.OrdinalIgnoreCase);
            }

            output["StatusCode"] = "OK";
            output["StatusMessage"] = "Success";
            output["Confidence"] = MeasurementStatisticsHelper.ComputeConfidenceFromUncertainty(aggregateStats.StdError);
            output["UncertaintyPx"] = aggregateStats.StdError;

            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
        finally
        {
            foreach (var analysisChannel in analysisChannels)
            {
                analysisChannel.Dispose();
            }
        }
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

    private static List<AnalysisChannel> ResolveAnalysisChannels(Mat src, string channel)
    {
        if (channel.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (src.Channels() == 1)
            {
                return new List<AnalysisChannel> { new("Gray", src.Clone()) };
            }

            var channels = src.Split();
            var channelNames = new[] { "B", "G", "R", "A" };
            var results = new List<AnalysisChannel>(channels.Length);
            for (var i = 0; i < channels.Length; i++)
            {
                var name = i < channelNames.Length ? channelNames[i] : $"C{i}";
                results.Add(new AnalysisChannel(name, channels[i]));
            }

            return results;
        }

        if (channel.Equals("Gray", StringComparison.OrdinalIgnoreCase))
        {
            return new List<AnalysisChannel> { new("Gray", ExtractGray(src)) };
        }

        if (src.Channels() == 1)
        {
            return new List<AnalysisChannel> { new("Gray", src.Clone()) };
        }

        var splitChannels = src.Split();
        try
        {
            var selected = channel.ToUpperInvariant() switch
            {
                "R" => splitChannels[2].Clone(),
                "G" => splitChannels[1].Clone(),
                "B" => splitChannels[0].Clone(),
                _ => splitChannels[0].Clone()
            };

            return new List<AnalysisChannel> { new(channel.ToUpperInvariant(), selected) };
        }
        finally
        {
            foreach (var c in splitChannels)
            {
                c.Dispose();
            }
        }
    }

    private static Mat ResolveMask(Dictionary<string, object>? inputs, Rect roi, Size sourceSize, out string? error)
    {
        error = null;
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

        Mat roiMask;
        if (grayMask.Size() == sourceSize)
        {
            if (roi.Right > grayMask.Width || roi.Bottom > grayMask.Height)
            {
                grayMask.Dispose();
                error = "Mask ROI exceeds mask image bounds";
                return new Mat();
            }

            roiMask = new Mat(grayMask, roi).Clone();
            grayMask.Dispose();
            grayMask = roiMask;
        }
        else if (grayMask.Size() != roi.Size)
        {
            grayMask.Dispose();
            error = "Mask must match the full image size or the resolved ROI size";
            return new Mat();
        }

        Cv2.Threshold(grayMask, grayMask, 1, 255, ThresholdTypes.Binary);
        return grayMask;
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

    private static List<double> ExtractValues(Mat analysis, Mat mask)
    {
        var values = new List<double>();
        for (var y = 0; y < analysis.Rows; y++)
        {
            for (var x = 0; x < analysis.Cols; x++)
            {
                if (!mask.Empty() && mask.At<byte>(y, x) == 0)
                {
                    continue;
                }

                values.Add(ReadScalarValue(analysis, x, y));
            }
        }

        return values;
    }

    private static double ReadScalarValue(Mat mat, int x, int y)
    {
        return mat.Depth() switch
        {
            MatType.CV_8U => mat.At<byte>(y, x),
            MatType.CV_8S => mat.At<sbyte>(y, x),
            MatType.CV_16U => mat.At<ushort>(y, x),
            MatType.CV_16S => mat.At<short>(y, x),
            MatType.CV_32S => mat.At<int>(y, x),
            MatType.CV_32F => mat.At<float>(y, x),
            MatType.CV_64F => mat.At<double>(y, x),
            _ => throw new NotSupportedException($"Unsupported image depth for pixel statistics: {mat.Depth()}.")
        };
    }

    private static StatisticsSummary ComputeStatistics(List<double> values)
    {
        if (values.Count == 0)
        {
            return new StatisticsSummary(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0, 0);
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        var sum = 0.0;
        var sumSquares = 0.0;
        var nonZeroCount = 0;

        foreach (var value in values)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
            sumSquares += value * value;
            if (value != 0.0)
            {
                nonZeroCount++;
            }
        }

        var mean = sum / values.Count;
        var variance = Math.Max(0.0, (sumSquares / values.Count) - (mean * mean));
        var stdDev = Math.Sqrt(variance);
        var median = MeasurementStatisticsHelper.ComputeMedian(values);
        var medianAbsoluteDeviation = MeasurementStatisticsHelper.ComputeMedianAbsoluteDeviation(values, median);
        var stdError = MeasurementStatisticsHelper.ComputeStandardError(stdDev, values.Count);

        return new StatisticsSummary(
            mean,
            stdDev,
            min,
            max,
            median,
            max - min,
            medianAbsoluteDeviation,
            stdError,
            nonZeroCount,
            values.Count);
    }

    private static Dictionary<string, object> CreateStatisticsDictionary(StatisticsSummary stats)
    {
        return new Dictionary<string, object>
        {
            { "Mean", stats.Mean },
            { "StdDev", stats.StdDev },
            { "Min", stats.Min },
            { "Max", stats.Max },
            { "Median", stats.Median },
            { "Range", stats.Range },
            { "MedianAbsoluteDeviation", stats.MedianAbsoluteDeviation },
            { "StdError", stats.StdError },
            { "NonZeroCount", stats.NonZeroCount },
            { "SampleCount", stats.SampleCount }
        };
    }

    private sealed record AnalysisChannel(string Name, Mat Data) : IDisposable
    {
        public void Dispose()
        {
            Data.Dispose();
        }
    }

    private sealed record StatisticsSummary(
        double Mean,
        double StdDev,
        double Min,
        double Max,
        double Median,
        double Range,
        double MedianAbsoluteDeviation,
        double StdError,
        int NonZeroCount,
        int SampleCount);
}
