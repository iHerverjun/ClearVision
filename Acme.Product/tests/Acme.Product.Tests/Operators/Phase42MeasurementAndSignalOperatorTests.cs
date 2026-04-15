using System.Numerics;
using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class Phase42MeasurementAndSignalOperatorTests
{
    [Fact]
    public async Task ArcCaliper_ShouldDetectArcEdges()
    {
        var sut = new ArcCaliperOperator(Substitute.For<ILogger<ArcCaliperOperator>>());
        var op = new Operator("ArcCaliper", OperatorType.ArcCaliper, 0, 0);
        using var image = CreateArcEdgeImage();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = image,
            ["CenterX"] = 100,
            ["CenterY"] = 100,
            ["Radius"] = 55,
            ["StartAngle"] = 20.0,
            ["EndAngle"] = 160.0,
            ["Transition"] = "positive"
        });

        result.IsSuccess.Should().BeTrue();
        Convert.ToInt32(result.OutputData!["Count"]).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ContourExtrema_ShouldReportMinAndMaxPoints()
    {
        var sut = new ContourExtremaOperator(Substitute.For<ILogger<ContourExtremaOperator>>());
        var op = new Operator("ContourExtrema", OperatorType.ContourExtrema, 0, 0);
        var contour = new[]
        {
            new Point2f(20, 40),
            new Point2f(10, 12),
            new Point2f(50, 18),
            new Point2f(40, 60)
        };

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Contour"] = contour,
            ["Direction"] = "vertical"
        });

        result.IsSuccess.Should().BeTrue();
        var minPoint = (Point2f)result.OutputData!["MinPoint"];
        var maxPoint = (Point2f)result.OutputData["MaxPoint"];
        minPoint.Y.Should().BeApproximately(12, 0.1f);
        maxPoint.Y.Should().BeApproximately(60, 0.1f);
    }

    [Fact]
    public async Task FftFilterAndInverseFft_ShouldPreserveSignalLength()
    {
        var fft = new FFT1DOperator(Substitute.For<ILogger<FFT1DOperator>>());
        var filter = new FrequencyFilterOperator(Substitute.For<ILogger<FrequencyFilterOperator>>());
        var inverse = new InverseFFT1DOperator(Substitute.For<ILogger<InverseFFT1DOperator>>());

        var signal = Enumerable.Range(0, 64)
            .Select(i => Math.Sin(2 * Math.PI * i / 8.0) + 0.25 * Math.Sin(2 * Math.PI * i / 3.0))
            .ToArray();

        var fftResult = await fft.ExecuteAsync(new Operator("FFT1D", OperatorType.FFT1D, 0, 0), new Dictionary<string, object>
        {
            ["Input"] = signal
        });
        fftResult.IsSuccess.Should().BeTrue();
        var spectrum = fftResult.OutputData!["Spectrum"].Should().BeOfType<Complex[]>().Subject;
        spectrum.Length.Should().Be(signal.Length);

        var filterResult = await filter.ExecuteAsync(new Operator("FrequencyFilter", OperatorType.FrequencyFilter, 0, 0), new Dictionary<string, object>
        {
            ["Spectrum"] = spectrum,
            ["FilterType"] = "lowpass",
            ["CutoffLow"] = 0.2,
            ["CutoffHigh"] = 0.5
        });
        filterResult.IsSuccess.Should().BeTrue();
        var filteredSpectrum = filterResult.OutputData!["FilteredSpectrum"].Should().BeOfType<Complex[]>().Subject;
        filteredSpectrum.Length.Should().Be(signal.Length);

        var inverseResult = await inverse.ExecuteAsync(new Operator("InverseFFT1D", OperatorType.InverseFFT1D, 0, 0), new Dictionary<string, object>
        {
            ["Spectrum"] = filteredSpectrum
        });
        inverseResult.IsSuccess.Should().BeTrue();
        var reconstructed = inverseResult.OutputData!["Signal"].Should().BeOfType<double[]>().Subject;
        reconstructed.Length.Should().Be(signal.Length);
    }

    [Fact]
    public async Task FftAndInverseFft_ShouldReconstructBinAlignedSignalWithinTolerance()
    {
        var fft = new FFT1DOperator(Substitute.For<ILogger<FFT1DOperator>>());
        var inverse = new InverseFFT1DOperator(Substitute.For<ILogger<InverseFFT1DOperator>>());

        const int sampleCount = 128;
        var signal = Enumerable.Range(0, sampleCount)
            .Select(i => 1.2 * Math.Sin(2 * Math.PI * 4 * i / sampleCount) + 0.35 * Math.Sin(2 * Math.PI * 12 * i / sampleCount))
            .ToArray();

        var fftResult = await fft.ExecuteAsync(new Operator("FFT1D", OperatorType.FFT1D, 0, 0), new Dictionary<string, object>
        {
            ["Input"] = signal
        });

        fftResult.IsSuccess.Should().BeTrue();

        var inverseResult = await inverse.ExecuteAsync(new Operator("InverseFFT1D", OperatorType.InverseFFT1D, 0, 0), new Dictionary<string, object>
        {
            ["Spectrum"] = fftResult.OutputData!["Spectrum"]
        });

        inverseResult.IsSuccess.Should().BeTrue();

        var reconstructed = inverseResult.OutputData!["Signal"].Should().BeOfType<double[]>().Subject;
        reconstructed.Length.Should().Be(sampleCount);

        var mse = signal.Zip(reconstructed, (expected, actual) => Math.Pow(expected - actual, 2)).Average();
        var maxAbs = signal.Zip(reconstructed, (expected, actual) => Math.Abs(expected - actual)).Max();

        mse.Should().BeLessThan(1e-6, "FFT/IFFT round-trip should preserve bin-aligned lab signal energy");
        maxAbs.Should().BeLessThan(5e-3, "FFT/IFFT round-trip should preserve sample amplitudes within mill-level tolerance");
    }

    [Fact]
    public async Task FrequencyOperators_LabBudget1024PointChain_ShouldStayWithinBudgetAndAttenuateHighFrequency()
    {
        var fft = new FFT1DOperator(Substitute.For<ILogger<FFT1DOperator>>());
        var filter = new FrequencyFilterOperator(Substitute.For<ILogger<FrequencyFilterOperator>>());
        var inverse = new InverseFFT1DOperator(Substitute.For<ILogger<InverseFFT1DOperator>>());

        const int sampleCount = 1024;
        const int iterations = 24;
        var signal = Enumerable.Range(0, sampleCount)
            .Select(i => Math.Sin(2 * Math.PI * 8 * i / sampleCount) + 0.45 * Math.Sin(2 * Math.PI * 192 * i / sampleCount))
            .ToArray();

        var elapsedSamples = new List<double>(iterations);
        double attenuationRatio = 1.0;

        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();

            var fftResult = await fft.ExecuteAsync(new Operator("FFT1D", OperatorType.FFT1D, 0, 0), new Dictionary<string, object>
            {
                ["Input"] = signal
            });
            fftResult.IsSuccess.Should().BeTrue();

            var spectrum = fftResult.OutputData!["Spectrum"].Should().BeOfType<Complex[]>().Subject;
            var originalHigh = spectrum[192].Magnitude;

            var filterResult = await filter.ExecuteAsync(new Operator("FrequencyFilter", OperatorType.FrequencyFilter, 0, 0), new Dictionary<string, object>
            {
                ["Spectrum"] = spectrum,
                ["FilterType"] = "lowpass",
                ["CutoffLow"] = 0.08,
                ["CutoffHigh"] = 0.2,
                ["Order"] = 4
            });
            filterResult.IsSuccess.Should().BeTrue();

            var filteredSpectrum = filterResult.OutputData!["FilteredSpectrum"].Should().BeOfType<Complex[]>().Subject;
            attenuationRatio = filteredSpectrum[192].Magnitude / originalHigh;

            var inverseResult = await inverse.ExecuteAsync(new Operator("InverseFFT1D", OperatorType.InverseFFT1D, 0, 0), new Dictionary<string, object>
            {
                ["Spectrum"] = filteredSpectrum
            });
            inverseResult.IsSuccess.Should().BeTrue();
            inverseResult.OutputData!["Signal"].Should().BeOfType<double[]>().Subject.Length.Should().Be(sampleCount);

            sw.Stop();
            elapsedSamples.Add(sw.Elapsed.TotalMilliseconds);
        }

        attenuationRatio.Should().BeLessThan(0.1, "low-pass filter should strongly attenuate high-frequency bin-aligned component");

        var averageMs = elapsedSamples.Average();
        var budgetMs = GetEnvDouble("CV_FREQUENCY_CHAIN_BUDGET_MS", 25.0, 5.0, 200.0);
        averageMs.Should().BeLessThan(budgetMs, $"1024-point FFT -> lowpass -> IFFT lab chain should stay within the configured audit budget ({budgetMs:0.##} ms)");
    }

    [Fact]
    public async Task PhaseClosure_ShouldReturnUnwrappedPhaseOutputs()
    {
        var sut = new PhaseClosureOperator(Substitute.For<ILogger<PhaseClosureOperator>>());
        var op = new Operator("PhaseClosure", OperatorType.PhaseClosure, 0, 0);
        using var phase = CreateWrappedPhaseImage();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PhaseImage"] = phase,
            ["UnwrapMethod"] = "itoh"
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("UnwrappedPhase");
        result.OutputData.Should().ContainKey("Quality");
    }

    [Fact]
    public async Task MorphologicalOperation_ShouldSupportTopHatAndBlackHat()
    {
        var sut = new MorphologicalOperationOperator(Substitute.For<ILogger<MorphologicalOperationOperator>>());

        foreach (var operation in new[] { "TopHat", "BlackHat" })
        {
            using var image = CreateMorphologyDetailImage();
            var op = new Operator($"Morph_{operation}", OperatorType.MorphologicalOperation, 0, 0);
            op.Parameters.Add(TestHelpers.CreateParameter("Operation", operation, "string"));

            var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

            result.IsSuccess.Should().BeTrue();
            result.OutputData.Should().ContainKey("Image");
            result.OutputData["Operation"].Should().Be(operation);
        }
    }

    private static ImageWrapper CreateArcEdgeImage()
    {
        var mat = new Mat(200, 200, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(100, 100), 55, Scalar.White, 4);
        return new ImageWrapper(mat);
    }

    private static Mat CreateWrappedPhaseImage()
    {
        var phase = new Mat(64, 64, MatType.CV_32FC1);
        for (var y = 0; y < phase.Rows; y++)
        {
            for (var x = 0; x < phase.Cols; x++)
            {
                var value = ((x * 0.25) + (y * 0.18)) % (2 * Math.PI);
                phase.Set(y, x, (float)value);
            }
        }

        return phase;
    }

    private static ImageWrapper CreateMorphologyDetailImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, new Scalar(30, 30, 30));
        Cv2.Rectangle(mat, new Rect(20, 20, 80, 60), new Scalar(70, 70, 70), -1);
        Cv2.Circle(mat, new Point(60, 50), 8, Scalar.White, -1);
        Cv2.Circle(mat, new Point(90, 80), 6, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static double GetEnvDouble(string name, double defaultValue, double minValue, double maxValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!double.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, minValue, maxValue);
    }
}
