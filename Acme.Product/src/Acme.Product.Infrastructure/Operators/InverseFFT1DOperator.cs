// InverseFFT1DOperator.cs
// 一维/图像逆 FFT 算子
// 对标: numpy.fft.ifft / OpenCV dft

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Numerics;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Inverse FFT 1D",
    Description = "Performs inverse FFT on 1D spectra and reconstructs images from 2D complex spectra.",
    Category = "Frequency",
    IconName = "ifft-1d",
    Keywords = new[] { "IFFT", "InverseFFT", "Fourier", "Reconstruction", "Signal" }
)]
[InputPort("Spectrum", "Input Frequency Spectrum", PortDataType.Any, IsRequired = true)]
[InputPort("OutputSize", "Desired Output Size", PortDataType.Integer, IsRequired = false)]
[OutputPort("Signal", "Reconstructed Signal", PortDataType.Any)]
[OutputPort("Real", "Real Part", PortDataType.Any)]
[OutputPort("Imaginary", "Imaginary Part", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class InverseFFT1DOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.InverseFFT1D;

    public InverseFFT1DOperator(ILogger<InverseFFT1DOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Spectrum", out var spectrumInput) != true || spectrumInput == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum required."));
        }

        var outputSize = GetInt(inputs, "OutputSize", 0);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (spectrumInput is Complex[] spectrum)
        {
            var count = outputSize > 0 ? Math.Min(outputSize, spectrum.Length) : spectrum.Length;
            var reconstructed = IFFTComplex(spectrum.Take(count).ToArray());
            var realSignal = reconstructed.Select(static value => value.Real).ToArray();
            var imaginarySignal = reconstructed.Select(static value => value.Imaginary).ToArray();
            var visualization = CreateSignalVisualization(realSignal);

            stopwatch.Stop();
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
            {
                { "Signal", realSignal },
                { "Real", realSignal },
                { "Imaginary", imaginarySignal },
                { "SignalLength", realSignal.Length },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }

        if (!TryResolveComplexSpectrum(spectrumInput, out var complexSpectrum, out var resolveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(resolveError));
        }

        using var complexSpectrumHandle = complexSpectrum;
        using var inverseComplex = new Mat();
        Cv2.Dft(complexSpectrumHandle, inverseComplex, DftFlags.Inverse | DftFlags.Scale | DftFlags.ComplexOutput);
        Cv2.Split(inverseComplex, out var channels);
        if (channels.Length != 2)
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }

            return Task.FromResult(OperatorExecutionOutput.Failure("Inverse FFT requires a 2-channel complex spectrum."));
        }

        var realMat = channels[0];
        var imaginaryMat = channels[1];
        var signalMat = realMat.Clone();
        var visualization2D = CreateImageVisualization(realMat);

        stopwatch.Stop();
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization2D, new Dictionary<string, object>
        {
            { "Signal", new ImageWrapper(signalMat) },
            { "Real", new ImageWrapper(realMat) },
            { "Imaginary", new ImageWrapper(imaginaryMat) },
            { "SignalLength", signalMat.Rows * signalMat.Cols },
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

    private static Complex[] IFFTComplex(IReadOnlyList<Complex> spectrum)
    {
        var data = new Vec2f[spectrum.Count];
        for (var i = 0; i < spectrum.Count; i++)
        {
            data[i] = new Vec2f((float)spectrum[i].Real, (float)spectrum[i].Imaginary);
        }

        using var src = new Mat(spectrum.Count, 1, MatType.CV_32FC2, data);
        using var dst = new Mat();
        Cv2.Dft(src, dst, DftFlags.Inverse | DftFlags.Scale | DftFlags.ComplexOutput);

        var result = new Complex[spectrum.Count];
        for (var i = 0; i < spectrum.Count; i++)
        {
            var value = dst.At<Vec2f>(i, 0);
            result[i] = new Complex(value.Item0, value.Item1);
        }

        return result;
    }

    private static Mat CreateSignalVisualization(IReadOnlyList<double> signal)
    {
        var width = Math.Max(512, signal.Count);
        const int height = 300;
        var visualization = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        var minValue = signal.Count == 0 ? 0.0 : signal.Min();
        var maxValue = signal.Count == 0 ? 1.0 : signal.Max();
        var range = Math.Max(maxValue - minValue, 1e-12);

        var centerY = height / 2;
        var previousX = 0;
        var previousY = centerY;

        for (var i = 0; i < signal.Count; i++)
        {
            var x = (int)(i * (double)width / Math.Max(signal.Count, 1));
            var y = centerY - (int)(100 * (signal[i] - minValue) / range - 50);
            if (i > 0)
            {
                Cv2.Line(visualization, new Point(previousX, previousY), new Point(x, y), new Scalar(0, 255, 0), 1);
            }

            previousX = x;
            previousY = y;
        }

        Cv2.Line(visualization, new Point(0, centerY), new Point(width, centerY), new Scalar(100, 100, 100), 1, LineTypes.Link8);
        Cv2.PutText(visualization, $"Signal (n={signal.Count})", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return visualization;
    }

    private static Mat CreateImageVisualization(Mat realSignal)
    {
        using var normalized = new Mat();
        Cv2.Normalize(realSignal, normalized, 0, 255, NormTypes.MinMax);
        normalized.ConvertTo(normalized, MatType.CV_8UC1);

        var visualization = new Mat();
        Cv2.ApplyColorMap(normalized, visualization, ColormapTypes.Bone);
        return visualization;
    }

    private static int GetInt(Dictionary<string, object>? inputs, string key, int defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true && value is int integer ? integer : defaultValue;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
