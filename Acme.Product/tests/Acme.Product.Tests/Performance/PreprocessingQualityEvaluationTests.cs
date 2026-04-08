using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.TestData;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Performance;

[Trait("Category", "Sprint7_Benchmark")]
public class PreprocessingQualityEvaluationTests
{
    [Fact]
    public async Task Evaluate_PreprocessingQuality_ShouldGenerateReferenceAndRealSampleReport()
    {
        var entries = new List<QualityEntry>();

        await EvaluateMedianBlurAgainstReference(entries);
        await EvaluateFrameAveragingAgainstReference(entries);
        await EvaluateClaheOnRealSample(entries);
        await EvaluateHistogramEqualizationOnRealSample(entries);
        await EvaluateShadingCorrectionOnRealSample(entries);

        entries.Should().NotBeEmpty();

        var reportPath = WriteReport(entries);
        File.Exists(reportPath).Should().BeTrue();
    }

    private static async Task EvaluateMedianBlurAgainstReference(ICollection<QualityEntry> entries)
    {
        var executor = new MedianBlurOperator(Substitute.For<ILogger<MedianBlurOperator>>());
        var op = new Operator("median", OperatorType.MedianBlur, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 3, "int"));

        using var clean = Cv2.ImRead(PreprocessingTestSupport.ResolveTestDataPath("shapes_composite.png"), ImreadModes.Grayscale);
        using var noisy = Cv2.ImRead(PreprocessingTestSupport.ResolveTestDataPath("shapes_composite_saltpepper.png"), ImreadModes.Grayscale);
        using var input = new ImageWrapper(noisy.Clone());

        var result = await executor.ExecuteAsync(op, TestHelpers.CreateImageInputs(input));
        result.IsSuccess.Should().BeTrue();

        using var output = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();

        var maeBefore = PreprocessingTestSupport.ComputeMeanAbsoluteError(noisy, clean);
        var maeAfter = PreprocessingTestSupport.ComputeMeanAbsoluteError(actual, clean);
        var psnrBefore = PreprocessingTestSupport.ComputePsnr(noisy, clean);
        var psnrAfter = PreprocessingTestSupport.ComputePsnr(actual, clean);

        maeAfter.Should().BeLessThan(maeBefore);
        psnrAfter.Should().BeGreaterThan(psnrBefore);

        entries.Add(new QualityEntry("reference_shapes_saltpepper", "MedianBlur", "MAE", maeBefore, maeAfter, "lower_is_better"));
        entries.Add(new QualityEntry("reference_shapes_saltpepper", "MedianBlur", "PSNR", psnrBefore, psnrAfter, "higher_is_better"));
    }

    private static async Task EvaluateFrameAveragingAgainstReference(ICollection<QualityEntry> entries)
    {
        var executor = new FrameAveragingOperator(Substitute.For<ILogger<FrameAveragingOperator>>());
        var op = new Operator("frame_avg", OperatorType.FrameAveraging, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FrameCount", 5, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Mean", "string"));

        using var clean = Cv2.ImRead(PreprocessingTestSupport.ResolveTestDataPath("shapes_composite.png"), ImreadModes.Grayscale);

        Mat? firstNoisy = null;
        ImageWrapper? finalOutput = null;

        try
        {
            for (var i = 0; i < 5; i++)
            {
                using var noisy = TestDataGenerator.AddGaussianNoise(clean, sigma: 18, new Random(1337 + i));
                firstNoisy ??= noisy.Clone();
                using var input = new ImageWrapper(noisy.Clone());

                var result = await executor.ExecuteAsync(op, TestHelpers.CreateImageInputs(input));
                result.IsSuccess.Should().BeTrue();

                finalOutput?.Dispose();
                finalOutput = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
            }

            finalOutput.Should().NotBeNull();
            using var actual = finalOutput!.GetMat();

            var maeBefore = PreprocessingTestSupport.ComputeMeanAbsoluteError(firstNoisy!, clean);
            var maeAfter = PreprocessingTestSupport.ComputeMeanAbsoluteError(actual, clean);
            var psnrBefore = PreprocessingTestSupport.ComputePsnr(firstNoisy!, clean);
            var psnrAfter = PreprocessingTestSupport.ComputePsnr(actual, clean);

            maeAfter.Should().BeLessThan(maeBefore);
            psnrAfter.Should().BeGreaterThan(psnrBefore);

            entries.Add(new QualityEntry("reference_shapes_frame_stack", "FrameAveraging", "MAE", maeBefore, maeAfter, "lower_is_better"));
            entries.Add(new QualityEntry("reference_shapes_frame_stack", "FrameAveraging", "PSNR", psnrBefore, psnrAfter, "higher_is_better"));
        }
        finally
        {
            firstNoisy?.Dispose();
            finalOutput?.Dispose();
        }
    }

    private static async Task EvaluateClaheOnRealSample(ICollection<QualityEntry> entries)
    {
        var executor = new ClaheEnhancementOperator(Substitute.For<ILogger<ClaheEnhancementOperator>>());
        var op = new Operator("clahe_real", OperatorType.ClaheEnhancement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ClipLimit", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("TileWidth", 8, "int"));
        op.AddParameter(TestHelpers.CreateParameter("TileHeight", 8, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ColorSpace", "Lab", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Channel", "Auto", "string"));

        using var sample = LoadRealSample();
        using var input = new ImageWrapper(sample.Clone());
        var result = await executor.ExecuteAsync(op, TestHelpers.CreateImageInputs(input));
        result.IsSuccess.Should().BeTrue();

        using var output = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();

        var contrastBefore = PreprocessingTestSupport.ComputeRmsContrast(sample);
        var contrastAfter = PreprocessingTestSupport.ComputeRmsContrast(actual);
        var entropyBefore = PreprocessingTestSupport.ComputeEntropy(sample);
        var entropyAfter = PreprocessingTestSupport.ComputeEntropy(actual);

        contrastAfter.Should().BeGreaterThan(contrastBefore);
        entropyAfter.Should().BeGreaterThan(entropyBefore);

        entries.Add(new QualityEntry("real_wire_sequence", "ClaheEnhancement", "RMSContrast", contrastBefore, contrastAfter, "higher_is_better"));
        entries.Add(new QualityEntry("real_wire_sequence", "ClaheEnhancement", "Entropy", entropyBefore, entropyAfter, "higher_is_better"));
    }

    private static async Task EvaluateHistogramEqualizationOnRealSample(ICollection<QualityEntry> entries)
    {
        var executor = new HistogramEqualizationOperator(Substitute.For<ILogger<HistogramEqualizationOperator>>());
        var op = new Operator("hist_real", OperatorType.HistogramEqualization, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "Global", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ApplyToEachChannel", false, "bool"));

        using var sample = LoadRealSample();
        using var input = new ImageWrapper(sample.Clone());
        var result = await executor.ExecuteAsync(op, TestHelpers.CreateImageInputs(input));
        result.IsSuccess.Should().BeTrue();

        using var output = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();

        var contrastBefore = PreprocessingTestSupport.ComputeRmsContrast(sample);
        var contrastAfter = PreprocessingTestSupport.ComputeRmsContrast(actual);
        var sharpnessBefore = PreprocessingTestSupport.ComputeSharpness(sample);
        var sharpnessAfter = PreprocessingTestSupport.ComputeSharpness(actual);

        contrastAfter.Should().BeGreaterThan(contrastBefore);
        sharpnessAfter.Should().BeGreaterThan(sharpnessBefore * 0.8);

        entries.Add(new QualityEntry("real_wire_sequence", "HistogramEqualization", "RMSContrast", contrastBefore, contrastAfter, "higher_is_better"));
        entries.Add(new QualityEntry("real_wire_sequence", "HistogramEqualization", "Sharpness", sharpnessBefore, sharpnessAfter, "higher_is_better"));
    }

    private static async Task EvaluateShadingCorrectionOnRealSample(ICollection<QualityEntry> entries)
    {
        var executor = new ShadingCorrectionOperator(Substitute.For<ILogger<ShadingCorrectionOperator>>());
        var op = new Operator("shading_real", OperatorType.ShadingCorrection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "GaussianModel", "string"));
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 51, "int"));

        using var sample = LoadRealSample();
        using var input = new ImageWrapper(sample.Clone());
        var result = await executor.ExecuteAsync(op, TestHelpers.CreateImageInputs(input));
        result.IsSuccess.Should().BeTrue();

        using var output = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();

        var flatnessBefore = PreprocessingTestSupport.ComputeIlluminationCoefficientOfVariation(sample);
        var flatnessAfter = PreprocessingTestSupport.ComputeIlluminationCoefficientOfVariation(actual);
        var sharpnessBefore = PreprocessingTestSupport.ComputeSharpness(sample);
        var sharpnessAfter = PreprocessingTestSupport.ComputeSharpness(actual);

        flatnessAfter.Should().BeLessThan(flatnessBefore);
        sharpnessAfter.Should().BeGreaterThan(sharpnessBefore * 0.2);

        entries.Add(new QualityEntry("real_wire_sequence", "ShadingCorrection", "IlluminationCV", flatnessBefore, flatnessAfter, "lower_is_better"));
        entries.Add(new QualityEntry("real_wire_sequence", "ShadingCorrection", "Sharpness", sharpnessBefore, sharpnessAfter, "higher_is_better"));
    }

    private static Mat LoadRealSample()
    {
        return Cv2.ImRead(
            PreprocessingTestSupport.ResolveWorkspacePath("线序检测", "unnamed.jpg"),
            ImreadModes.Color);
    }

    private static string WriteReport(IReadOnlyList<QualityEntry> entries)
    {
        var reportPath = Path.Combine(PreprocessingTestSupport.EnsureReportDirectory(), "preprocessing_quality_report.md");
        var builder = new StringBuilder();
        builder.AppendLine("# Preprocessing Quality Report");
        builder.AppendLine();
        builder.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("| Case | Operator | Metric | Before | After | Delta | Expectation |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---|");

        foreach (var entry in entries)
        {
            builder.AppendLine(
                $"| {entry.CaseName} | {entry.OperatorName} | {entry.MetricName} | {entry.Before:F4} | {entry.After:F4} | {entry.After - entry.Before:F4} | {entry.Expectation} |");
        }

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    private sealed record QualityEntry(
        string CaseName,
        string OperatorName,
        string MetricName,
        double Before,
        double After,
        string Expectation);
}
