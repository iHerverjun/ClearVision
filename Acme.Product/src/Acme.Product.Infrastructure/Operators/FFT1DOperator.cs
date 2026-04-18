// FFT1DOperator.cs
// 一维/图像频谱算子
// 对标: numpy.fft.fft / OpenCV dft

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Numerics;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "FFT 1D",
    Description = "Performs FFT on 1D signals and emits a full complex spectrum for image inputs.",
    Category = "Frequency",
    IconName = "fft-1d",
    Keywords = new[] { "FFT", "Fourier", "Frequency", "Spectrum", "DFT" }
)]
[InputPort("Input", "Input Signal or Image", PortDataType.Any, IsRequired = true)]
[InputPort("Axis", "Transform Axis (legacy parameter for signal/image profiles)", PortDataType.Integer, IsRequired = false)]
[OutputPort("Spectrum", "Frequency Spectrum", PortDataType.Any)]
[OutputPort("Magnitude", "Magnitude Spectrum", PortDataType.Any)]
[OutputPort("Phase", "Phase Spectrum", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class FFT1DOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.FFT1D;

    public FFT1DOperator(ILogger<FFT1DOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Input", out var input) != true || input == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input required."));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (TryConvertToSignal(input, out var signal))
        {
            var spectrum = FFT(signal);
            var magnitudes = spectrum.Select(c => c.Magnitude).ToArray();
            var phases = spectrum.Select(c => c.Phase).ToArray();
            var vis = CreateSignalSpectrumVisualization(magnitudes, phases);

            stopwatch.Stop();
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
            {
                { "Spectrum", spectrum },
                { "Magnitude", magnitudes },
                { "Phase", phases },
                { "TransformKind", "1DSignal" },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }

        if (!ImageWrapper.TryGetFromObject(input, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input must be a numeric 1D array or image."));
        }

        var complexSpectrum = ComputeImageSpectrum(imageWrapper.GetMat());
        var magnitude = CreateMagnitudeSpectrum(complexSpectrum, logScale: false);
        var phase = CreatePhaseSpectrum(complexSpectrum);
        var visualization = CreateSpectrumVisualization2D(complexSpectrum);

        stopwatch.Stop();
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
        {
            { "Spectrum", new ImageWrapper(complexSpectrum) },
            { "Magnitude", new ImageWrapper(magnitude) },
            { "Phase", new ImageWrapper(phase) },
            { "TransformKind", "2DImage" },
            { "IsShifted", false },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private static bool TryConvertToSignal(object input, out double[] signal)
    {
        signal = Array.Empty<double>();

        switch (input)
        {
            case double[] doubles:
                signal = doubles;
                return true;
            case float[] singles:
                signal = singles.Select(static value => (double)value).ToArray();
                return true;
            case int[] integers:
                signal = integers.Select(static value => (double)value).ToArray();
                return true;
            default:
                return false;
        }
    }

    private static Complex[] FFT(IReadOnlyList<double> input)
    {
        var complexData = new Vec2f[input.Count];
        for (var i = 0; i < input.Count; i++)
        {
            complexData[i] = new Vec2f((float)input[i], 0f);
        }

        using var src = new Mat(input.Count, 1, MatType.CV_32FC2, complexData);
        using var dst = new Mat();
        Cv2.Dft(src, dst, DftFlags.ComplexOutput);

        var result = new Complex[input.Count];
        for (var i = 0; i < input.Count; i++)
        {
            var value = dst.At<Vec2f>(i, 0);
            result[i] = new Complex(value.Item0, value.Item1);
        }

        return result;
    }

    private static Mat ComputeImageSpectrum(Mat image)
    {
        using var singleChannelFloat = ConvertToSingleChannelFloat(image);
        var complexSpectrum = new Mat();
        Cv2.Dft(singleChannelFloat, complexSpectrum, DftFlags.ComplexOutput);
        return complexSpectrum;
    }

    private static Mat ConvertToSingleChannelFloat(Mat image)
    {
        using var gray = image.Channels() == 1
            ? image.Clone()
            : image.CvtColor(ColorConversionCodes.BGR2GRAY);

        var scale = gray.Depth() switch
        {
            MatType.CV_8U => 1.0,
            MatType.CV_16U => 1.0,
            MatType.CV_32F => 1.0,
            MatType.CV_64F => 1.0,
            _ => 1.0
        };

        var output = new Mat();
        gray.ConvertTo(output, MatType.CV_32FC1, scale);
        return output;
    }

    private static Mat CreateMagnitudeSpectrum(Mat complexSpectrum, bool logScale)
    {
        Cv2.Split(complexSpectrum, out var channels);
        try
        {
            var magnitude = new Mat();
            Cv2.Magnitude(channels[0], channels[1], magnitude);
            if (logScale)
            {
                Cv2.Add(magnitude, Scalar.All(1.0), magnitude);
                Cv2.Log(magnitude, magnitude);
            }

            return magnitude;
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat CreatePhaseSpectrum(Mat complexSpectrum)
    {
        Cv2.Split(complexSpectrum, out var channels);
        try
        {
            var phase = new Mat();
            Cv2.Phase(channels[0], channels[1], phase);
            return phase;
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat CreateSpectrumVisualization2D(Mat complexSpectrum)
    {
        using var magnitude = CreateMagnitudeSpectrum(complexSpectrum, logScale: true);
        using var shiftedMagnitude = ShiftQuadrants(magnitude);
        using var normalized = new Mat();

        Cv2.Normalize(shiftedMagnitude, normalized, 0, 255, NormTypes.MinMax);
        normalized.ConvertTo(normalized, MatType.CV_8UC1);

        var visualization = new Mat();
        Cv2.ApplyColorMap(normalized, visualization, ColormapTypes.Jet);
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

    private static Mat CreateSignalSpectrumVisualization(IReadOnlyList<double> magnitudes, IReadOnlyList<double> phases)
    {
        var width = Math.Max(512, magnitudes.Count * 2);
        const int height = 300;
        var visualization = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        var maxMagnitude = magnitudes.Count == 0 ? 1.0 : Math.Max(magnitudes.Max(), 1e-12);
        const double maxPhase = Math.PI;

        var magnitudeCenterY = height / 2 - 20;
        for (var i = 0; i < magnitudes.Count - 1; i++)
        {
            var x1 = (int)(i * (double)width / magnitudes.Count);
            var x2 = (int)((i + 1) * (double)width / magnitudes.Count);
            var y1 = magnitudeCenterY - (int)(100 * magnitudes[i] / maxMagnitude);
            var y2 = magnitudeCenterY - (int)(100 * magnitudes[i + 1] / maxMagnitude);
            Cv2.Line(visualization, new Point(x1, y1), new Point(x2, y2), new Scalar(0, 255, 0), 1);
        }

        var phaseCenterY = height - 50;
        for (var i = 0; i < phases.Count - 1; i++)
        {
            var x1 = (int)(i * (double)width / phases.Count);
            var x2 = (int)((i + 1) * (double)width / phases.Count);
            var y1 = phaseCenterY - (int)(40 * phases[i] / maxPhase);
            var y2 = phaseCenterY - (int)(40 * phases[i + 1] / maxPhase);
            Cv2.Line(visualization, new Point(x1, y1), new Point(x2, y2), new Scalar(255, 0, 255), 1);
        }

        Cv2.PutText(visualization, "Magnitude", new Point(10, 30), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
        Cv2.PutText(visualization, "Phase", new Point(10, height - 70), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 0, 255), 1);
        return visualization;
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
