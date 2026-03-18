// FrequencyFilterOperator.cs
// 频域滤波算子 - 在频域应用各种滤波器
// 对标: scipy.signal.filtfilt, Halcon frequency filtering

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
    Description = "Applies frequency domain filters (low-pass, high-pass, band-pass, band-stop) to spectrum.",
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
    public override OperatorType OperatorType => OperatorType.FrequencyFilter;

    public FrequencyFilterOperator(ILogger<FrequencyFilterOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Spectrum", out var spec) != true || spec == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum required."));

        string filterType = GetString(inputs, "FilterType", "lowpass").ToLower();
        double cutoffLow = GetDouble(inputs, "CutoffLow", 0.1);
        double cutoffHigh = GetDouble(inputs, "CutoffHigh", 0.3);
        int order = GetInt(inputs, "Order", 2);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (spec is Complex[] spectrum)
        {
            // 1D 频谱滤波
            int n = spectrum.Length;
            var mask = CreateFilterMask1D(filterType, n, cutoffLow, cutoffHigh, order);
            var filtered = new Complex[n];

            for (int i = 0; i < n; i++)
                filtered[i] = spectrum[i] * mask[i];

            stopwatch.Stop();

            var vis = CreateFilterVisualization1D(mask, filterType);

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
            {
                { "FilteredSpectrum", filtered },
                { "FilterMask", mask },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }
        else if (spec is Mat spectrum2D)
        {
            // 2D 频谱滤波 (图像)
            var mask2D = CreateFilterMask2D(filterType, spectrum2D.Size(), cutoffLow, cutoffHigh, order);
            using var filtered2D = new Mat();
            Cv2.Multiply(spectrum2D, mask2D, filtered2D);

            stopwatch.Stop();

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(mask2D, new Dictionary<string, object>
            {
                { "FilteredSpectrum", filtered2D },
                { "FilterMask", mask2D },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }

        return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum must be Complex[] or Mat."));
    }

    private double[] CreateFilterMask1D(string type, int n, double cutoffLow, double cutoffHigh, int order)
    {
        var mask = new double[n];
        var freqs = Enumerable.Range(0, n).Select(i => (double)i / n).ToArray();

        for (int i = 0; i < n; i++)
        {
            double f = freqs[i];
            mask[i] = type switch
            {
                "lowpass" or "low" => 1.0 / (1.0 + Math.Pow(f / cutoffLow, 2 * order)),
                "highpass" or "high" => Math.Pow(f / cutoffLow, 2 * order) / (1.0 + Math.Pow(f / cutoffLow, 2 * order)),
                "bandpass" or "band" => Math.Exp(-Math.Pow((f - (cutoffLow + cutoffHigh) / 2) / ((cutoffHigh - cutoffLow) / 2), 2)),
                "bandstop" or "notch" => 1.0 - Math.Exp(-Math.Pow((f - (cutoffLow + cutoffHigh) / 2) / ((cutoffHigh - cutoffLow) / 2), 2)),
                _ => 1.0
            };
        }

        return mask;
    }

    private Mat CreateFilterMask2D(string type, Size size, double cutoffLow, double cutoffHigh, int order)
    {
        var mask = new Mat(size, MatType.CV_32FC1, Scalar.All(1));
        int cx = size.Width / 2;
        int cy = size.Height / 2;

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                // 归一化频率 (0-1)
                double dx = (x - cx) / (double)cx;
                double dy = (y - cy) / (double)cy;
                double f = Math.Sqrt(dx * dx + dy * dy);

                double val = type switch
                {
                    "lowpass" or "low" => f <= cutoffLow ? 1.0 : Math.Exp(-Math.Pow((f - cutoffLow) / 0.1, 2)),
                    "highpass" or "high" => f >= cutoffLow ? 1.0 : 0.0,
                    "bandpass" or "band" => (f >= cutoffLow && f <= cutoffHigh) ? 1.0 : 0.0,
                    "bandstop" or "notch" => (f >= cutoffLow && f <= cutoffHigh) ? 0.0 : 1.0,
                    _ => 1.0
                };

                mask.At<float>(y, x) = (float)val;
            }
        }

        return mask;
    }

    private Mat CreateFilterVisualization1D(double[] mask, string filterType)
    {
        int width = 512, height = 200;
        var vis = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // 绘制滤波器响应曲线
        for (int i = 0; i < mask.Length - 1; i++)
        {
            int x1 = (int)(i * (double)width / mask.Length);
            int x2 = (int)((i + 1) * (double)width / mask.Length);
            int y1 = height - 20 - (int)(150 * mask[i]);
            int y2 = height - 20 - (int)(150 * mask[i + 1]);
            Cv2.Line(vis, new Point(x1, y1), new Point(x2, y2), new Scalar(0, 255, 255), 2);
        }

        Cv2.PutText(vis, $"Filter: {filterType}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 2);
        return vis;
    }

    private string GetString(Dictionary<string, object>? inputs, string key, string defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true ? v?.ToString() ?? defaultVal : defaultVal;

    private double GetDouble(Dictionary<string, object>? inputs, string key, double defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true ? Convert.ToDouble(v) : defaultVal;

    private int GetInt(Dictionary<string, object>? inputs, string key, int defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true && v is int i ? i : defaultVal;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
