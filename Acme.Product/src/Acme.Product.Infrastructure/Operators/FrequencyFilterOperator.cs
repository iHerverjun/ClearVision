// FrequencyFilterOperator.cs
// 频域滤波算子 - 兼容 1D 复频谱与 2D 复频谱

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Numerics;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Frequency Filter",
    Description = "Applies frequency-domain filters to 1D or 2D complex spectra.",
    Category = "Frequency",
    IconName = "frequency-filter",
    Keywords = new[] { "FFT", "Filter", "LowPass", "HighPass", "BandPass", "Frequency" }
)]
[InputPort("Spectrum", "Input Frequency Spectrum", PortDataType.Any, IsRequired = true)]
[InputPort("FilterType", "Filter Type", PortDataType.String, IsRequired = true)]
[InputPort("CutoffLow", "Low Cutoff Frequency", PortDataType.Float, IsRequired = false)]
[InputPort("CutoffHigh", "High Cutoff Frequency", PortDataType.Float, IsRequired = false)]
[InputPort("Order", "Filter Order", PortDataType.Integer, IsRequired = false)]
[OutputPort("FilteredSpectrum", "Filtered Spectrum", PortDataType.Any)]
[OutputPort("FilterMask", "Filter Mask", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class FrequencyFilterOperator : OperatorBase
{
    private const double MinCutoff = 1e-6;
    private const double MaxNormalizedCutoff = 0.5;

    public override OperatorType OperatorType => OperatorType.FrequencyFilter;

    public FrequencyFilterOperator(ILogger<FrequencyFilterOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Spectrum", out var spectrumInput) != true || spectrumInput == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum required."));
        }

        var filterType = GetString(inputs, "FilterType", "lowpass").Trim().ToLowerInvariant();
        var cutoffLow = NormalizeCutoff(GetDouble(inputs, "CutoffLow", 0.1));
        var cutoffHigh = NormalizeCutoff(GetDouble(inputs, "CutoffHigh", 0.3));
        var order = Math.Max(1, GetInt(inputs, "Order", 2));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (spectrumInput is Complex[] spectrum)
        {
            var mask = CreateFilterMask1D(filterType, spectrum.Length, cutoffLow, cutoffHigh, order);
            var filtered = new Complex[spectrum.Length];

            for (var i = 0; i < spectrum.Length; i++)
            {
                filtered[i] = spectrum[i] * mask[i];
            }

            stopwatch.Stop();
            var visualization = CreateFilterVisualization1D(mask, filterType);
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
            {
                { "FilteredSpectrum", filtered },
                { "FilterMask", mask },
                { "SpectrumKind", "1DSignal" },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }

        if (!TryResolveComplexSpectrum(spectrumInput, out var complexSpectrum, out var resolveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(resolveError));
        }

        using var complexSpectrumHandle = complexSpectrum;
        var mask2D = CreateFilterMask2D(filterType, complexSpectrumHandle.Size(), cutoffLow, cutoffHigh, order);
        var filteredSpectrum = ApplyComplexMask(complexSpectrumHandle, mask2D);
        var visualization2D = CreateMaskVisualization(mask2D);

        stopwatch.Stop();
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization2D, new Dictionary<string, object>
        {
            { "FilteredSpectrum", new ImageWrapper(filteredSpectrum) },
            { "FilterMask", new ImageWrapper(mask2D) },
            { "SpectrumKind", "2DComplexImage" },
            { "IsShifted", false },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private static bool TryResolveComplexSpectrum(object value, out Mat spectrum, out string error)
    {
        spectrum = new Mat();
        error = string.Empty;

        if (!ImageWrapper.TryGetFromObject(value, out var wrapper) || wrapper == null)
        {
            error = "Spectrum must be Complex[] or a 2-channel complex image.";
            return false;
        }

        var mat = wrapper.GetMat();
        if (mat.Empty())
        {
            error = "Spectrum image is empty.";
            return false;
        }

        if (mat.Channels() != 2)
        {
            error = "Spectrum image must be a 2-channel complex spectrum.";
            return false;
        }

        if (mat.Type() == MatType.CV_32FC2)
        {
            spectrum = mat.Clone();
            return true;
        }

        spectrum = new Mat();
        mat.ConvertTo(spectrum, MatType.CV_32FC2);
        return true;
    }

    private static Mat ApplyComplexMask(Mat complexSpectrum, Mat mask)
    {
        using var maskChannel2 = new Mat();
        Cv2.Merge(new[] { mask, mask }, maskChannel2);

        var filtered = new Mat();
        Cv2.Multiply(complexSpectrum, maskChannel2, filtered);
        return filtered;
    }

    private static double[] CreateFilterMask1D(string type, int sampleCount, double cutoffLow, double cutoffHigh, int order)
    {
        var mask = new double[sampleCount];
        var bandLow = Math.Min(cutoffLow, cutoffHigh);
        var bandHigh = Math.Max(cutoffLow, cutoffHigh);

        for (var i = 0; i < sampleCount; i++)
        {
            var frequency = Math.Abs(SignedFrequency(i, sampleCount));
            mask[i] = EvaluateFilter(type, frequency, bandLow, bandHigh, order);
        }

        return mask;
    }

    private static Mat CreateFilterMask2D(string type, Size size, double cutoffLow, double cutoffHigh, int order)
    {
        var mask = new Mat(size, MatType.CV_32FC1, Scalar.All(0));
        var bandLow = Math.Min(cutoffLow, cutoffHigh);
        var bandHigh = Math.Max(cutoffLow, cutoffHigh);

        for (var y = 0; y < size.Height; y++)
        {
            var fy = SignedFrequency(y, size.Height);
            for (var x = 0; x < size.Width; x++)
            {
                var fx = SignedFrequency(x, size.Width);
                var radius = Math.Sqrt((fx * fx) + (fy * fy));
                mask.Set(y, x, (float)EvaluateFilter(type, radius, bandLow, bandHigh, order));
            }
        }

        return mask;
    }

    private static double EvaluateFilter(string type, double normalizedFrequency, double cutoffLow, double cutoffHigh, int order)
    {
        return type switch
        {
            "lowpass" or "low" => ButterworthLowpass(normalizedFrequency, cutoffLow, order),
            "highpass" or "high" => ButterworthHighpass(normalizedFrequency, cutoffLow, order),
            "bandpass" or "band" => ButterworthHighpass(normalizedFrequency, cutoffLow, order) * ButterworthLowpass(normalizedFrequency, cutoffHigh, order),
            "bandstop" or "notch" => 1.0 - (ButterworthHighpass(normalizedFrequency, cutoffLow, order) * ButterworthLowpass(normalizedFrequency, cutoffHigh, order)),
            _ => 1.0
        };
    }

    private static double ButterworthLowpass(double normalizedFrequency, double cutoff, int order)
    {
        var safeCutoff = NormalizeCutoff(cutoff);
        if (normalizedFrequency <= 0)
        {
            return 1.0;
        }

        return 1.0 / (1.0 + Math.Pow(normalizedFrequency / safeCutoff, 2 * order));
    }

    private static double ButterworthHighpass(double normalizedFrequency, double cutoff, int order)
    {
        var safeCutoff = NormalizeCutoff(cutoff);
        if (normalizedFrequency <= 0)
        {
            return 0.0;
        }

        var ratio = Math.Pow(normalizedFrequency / safeCutoff, 2 * order);
        return ratio / (1.0 + ratio);
    }

    private static double NormalizeCutoff(double cutoff)
    {
        return Math.Clamp(cutoff, MinCutoff, MaxNormalizedCutoff);
    }

    private static double SignedFrequency(int index, int sampleCount)
    {
        return index <= sampleCount / 2
            ? (double)index / sampleCount
            : (double)(index - sampleCount) / sampleCount;
    }

    private static Mat CreateMaskVisualization(Mat mask)
    {
        using var shifted = ShiftQuadrants(mask);
        using var normalized = new Mat();
        Cv2.Normalize(shifted, normalized, 0, 255, NormTypes.MinMax);
        normalized.ConvertTo(normalized, MatType.CV_8UC1);

        var visualization = new Mat();
        Cv2.ApplyColorMap(normalized, visualization, ColormapTypes.Turbo);
        return visualization;
    }

    private static Mat ShiftQuadrants(Mat source)
    {
        var shifted = new Mat(source.Size(), source.Type(), Scalar.All(0));
        var centerX = source.Cols / 2;
        var centerY = source.Rows / 2;

        for (var y = 0; y < source.Rows; y++)
        {
            var srcY = (y + centerY) % source.Rows;
            for (var x = 0; x < source.Cols; x++)
            {
                var srcX = (x + centerX) % source.Cols;
                shifted.Set(y, x, source.At<float>(srcY, srcX));
            }
        }

        return shifted;
    }

    private static Mat CreateFilterVisualization1D(IReadOnlyList<double> mask, string filterType)
    {
        const int width = 512;
        const int height = 200;
        var visualization = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        for (var i = 0; i < mask.Count - 1; i++)
        {
            var x1 = (int)(i * (double)width / mask.Count);
            var x2 = (int)((i + 1) * (double)width / mask.Count);
            var y1 = height - 20 - (int)(150 * mask[i]);
            var y2 = height - 20 - (int)(150 * mask[i + 1]);
            Cv2.Line(visualization, new Point(x1, y1), new Point(x2, y2), new Scalar(0, 255, 255), 2);
        }

        Cv2.PutText(visualization, $"Filter: {filterType}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 2);
        return visualization;
    }

    private static string GetString(Dictionary<string, object>? inputs, string key, string defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true ? value?.ToString() ?? defaultValue : defaultValue;

    private static double GetDouble(Dictionary<string, object>? inputs, string key, double defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true ? Convert.ToDouble(value) : defaultValue;

    private static int GetInt(Dictionary<string, object>? inputs, string key, int defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true && value is int integer ? integer : defaultValue;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
