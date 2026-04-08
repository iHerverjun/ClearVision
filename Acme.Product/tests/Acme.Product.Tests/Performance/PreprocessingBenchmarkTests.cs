using System.Diagnostics;
using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Performance;

[Trait("Category", "Sprint7_Benchmark")]
public class PreprocessingBenchmarkTests
{
    private static readonly (string Label, int? Width, int? Height)[] BenchmarkSizes =
    [
        ("native", null, null),
        ("1920x1080", 1920, 1080),
        ("4096x3072", 4096, 3072)
    ];

    private static readonly BenchmarkCase[] BenchmarkCases =
    [
        new(
            "MedianBlur",
            () => new MedianBlurOperator(Substitute.For<ILogger<MedianBlurOperator>>()),
            () =>
            {
                var op = new Operator("median", OperatorType.MedianBlur, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("KernelSize", 3, "int"));
                return op;
            }),
        new(
            "BilateralFilter",
            () => new BilateralFilterOperator(Substitute.For<ILogger<BilateralFilterOperator>>()),
            () =>
            {
                var op = new Operator("bilateral", OperatorType.BilateralFilter, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("Diameter", 9, "int"));
                op.AddParameter(TestHelpers.CreateParameter("SigmaColor", 75.0, "double"));
                op.AddParameter(TestHelpers.CreateParameter("SigmaSpace", 75.0, "double"));
                return op;
            }),
        new(
            "ClaheEnhancement",
            () => new ClaheEnhancementOperator(Substitute.For<ILogger<ClaheEnhancementOperator>>()),
            () =>
            {
                var op = new Operator("clahe", OperatorType.ClaheEnhancement, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("ClipLimit", 2.0, "double"));
                op.AddParameter(TestHelpers.CreateParameter("TileWidth", 8, "int"));
                op.AddParameter(TestHelpers.CreateParameter("TileHeight", 8, "int"));
                op.AddParameter(TestHelpers.CreateParameter("ColorSpace", "Lab", "string"));
                op.AddParameter(TestHelpers.CreateParameter("Channel", "Auto", "string"));
                return op;
            }),
        new(
            "HistogramEqualization",
            () => new HistogramEqualizationOperator(Substitute.For<ILogger<HistogramEqualizationOperator>>()),
            () =>
            {
                var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("Method", "CLAHE", "string"));
                op.AddParameter(TestHelpers.CreateParameter("ClipLimit", 2.0, "double"));
                op.AddParameter(TestHelpers.CreateParameter("TileGridSize", 8, "int"));
                op.AddParameter(TestHelpers.CreateParameter("ApplyToEachChannel", false, "bool"));
                return op;
            }),
        new(
            "ShadingCorrection",
            () => new ShadingCorrectionOperator(Substitute.For<ILogger<ShadingCorrectionOperator>>()),
            () =>
            {
                var op = new Operator("shading", OperatorType.ShadingCorrection, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("Method", "GaussianModel", "string"));
                op.AddParameter(TestHelpers.CreateParameter("KernelSize", 51, "int"));
                return op;
            }),
        new(
            "AdaptiveThreshold",
            () => new AdaptiveThresholdOperator(Substitute.For<ILogger<AdaptiveThresholdOperator>>()),
            () =>
            {
                var op = new Operator("adaptive", OperatorType.AdaptiveThreshold, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("AdaptiveMethod", "Gaussian", "string"));
                op.AddParameter(TestHelpers.CreateParameter("ThresholdType", "Binary", "string"));
                op.AddParameter(TestHelpers.CreateParameter("BlockSize", 11, "int"));
                op.AddParameter(TestHelpers.CreateParameter("C", 2.0, "double"));
                return op;
            }),
        new(
            "FrameAveraging",
            () => new FrameAveragingOperator(Substitute.For<ILogger<FrameAveragingOperator>>()),
            () =>
            {
                var op = new Operator("frame_avg", OperatorType.FrameAveraging, 0, 0);
                op.AddParameter(TestHelpers.CreateParameter("FrameCount", 5, "int"));
                op.AddParameter(TestHelpers.CreateParameter("Mode", "Mean", "string"));
                return op;
            })
    ];

    [Fact]
    public async Task Benchmark_PreprocessingOperators_ShouldGenerateReport()
    {
        var entries = new List<BenchmarkEntry>();
        using var sample = LoadBenchmarkSample();

        foreach (var size in BenchmarkSizes)
        {
            using var benchmarkImage = ResizeForBenchmark(sample, size.Width, size.Height);
            var iterations = size.Width.GetValueOrDefault(sample.Width) >= 4000 ? 3 : 6;
            var warmups = size.Width.GetValueOrDefault(sample.Width) >= 4000 ? 2 : 3;

            foreach (var benchmarkCase in BenchmarkCases)
            {
                var samples = await RunBenchmarkAsync(benchmarkCase, benchmarkImage, warmups, iterations);
                samples.Should().NotBeEmpty();

                entries.Add(new BenchmarkEntry(
                    benchmarkCase.Name,
                    size.Label,
                    iterations,
                    samples.Average(),
                    Percentile(samples, 0.95),
                    Percentile(samples, 0.99),
                    samples.Max()));
            }
        }

        var reportPath = WriteReport(entries);
        File.Exists(reportPath).Should().BeTrue();
    }

    private static async Task<List<long>> RunBenchmarkAsync(
        BenchmarkCase benchmarkCase,
        Mat baseImage,
        int warmups,
        int iterations)
    {
        var executor = benchmarkCase.CreateExecutor();
        var op = benchmarkCase.CreateOperator();
        var samples = new List<long>(iterations);

        for (var i = 0; i < warmups; i++)
        {
            _ = await ExecuteOnceAsync(executor, op, baseImage);
        }

        for (var i = 0; i < iterations; i++)
        {
            samples.Add(await ExecuteOnceAsync(executor, op, baseImage));
        }

        return samples;
    }

    private static async Task<long> ExecuteOnceAsync(
        IOperatorExecutor executor,
        Operator @operator,
        Mat baseImage)
    {
        using var input = new ImageWrapper(baseImage.Clone());
        var stopwatch = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(@operator, TestHelpers.CreateImageInputs(input));
        stopwatch.Stop();

        result.IsSuccess.Should().BeTrue();
        PreprocessingTestSupport.DisposeObjectGraph(result.OutputData);
        return stopwatch.ElapsedMilliseconds;
    }

    private static Mat LoadBenchmarkSample()
    {
        var sample = Cv2.ImRead(
            PreprocessingTestSupport.ResolveWorkspacePath("线序检测", "unnamed.jpg"),
            ImreadModes.Color);
        sample.Empty().Should().BeFalse();
        return sample;
    }

    private static Mat ResizeForBenchmark(Mat src, int? width, int? height)
    {
        if (!width.HasValue || !height.HasValue)
        {
            return src.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(src, resized, new Size(width.Value, height.Value), 0, 0, InterpolationFlags.Linear);
        return resized;
    }

    private static double Percentile(IReadOnlyList<long> values, double percentile)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static string WriteReport(IReadOnlyList<BenchmarkEntry> entries)
    {
        var reportPath = Path.Combine(PreprocessingTestSupport.EnsureReportDirectory(), "preprocessing_benchmark_report.md");
        var builder = new StringBuilder();

        builder.AppendLine("# Preprocessing Benchmark Report");
        builder.AppendLine();
        builder.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Max (ms) |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|");

        foreach (var entry in entries.OrderBy(item => item.ResolutionLabel).ThenBy(item => item.AverageMs))
        {
            builder.AppendLine(
                $"| {entry.OperatorName} | {entry.ResolutionLabel} | {entry.Iterations} | {entry.AverageMs:F2} | {entry.P95Ms:F2} | {entry.P99Ms:F2} | {entry.MaxMs:F2} |");
        }

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    private sealed record BenchmarkCase(
        string Name,
        Func<IOperatorExecutor> CreateExecutor,
        Func<Operator> CreateOperator);

    private sealed record BenchmarkEntry(
        string OperatorName,
        string ResolutionLabel,
        int Iterations,
        double AverageMs,
        double P95Ms,
        double P99Ms,
        double MaxMs);
}
