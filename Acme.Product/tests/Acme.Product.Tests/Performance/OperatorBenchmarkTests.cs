using System.Collections;
using System.Diagnostics;
using System.Text;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit.Abstractions;
using CvPoint = OpenCvSharp.Point;

namespace Acme.Product.Tests.Performance;

[Trait("Category", "Sprint7_Benchmark")]
public class OperatorBenchmarkTests
{
    private static readonly (int Width, int Height)[] BenchmarkSizes =
    [
        (1920, 1080),
        (4096, 3072)
    ];

    private static readonly OperatorType[] BenchmarkTypes =
    [
        OperatorType.Filtering,
        OperatorType.Thresholding,
        OperatorType.EdgeDetection,
        OperatorType.Morphology,
        OperatorType.BlobAnalysis,
        OperatorType.SharpnessEvaluation
    ];

    private readonly ITestOutputHelper _output;
    private readonly OperatorFactory _factory;
    private readonly IReadOnlyDictionary<OperatorType, IOperatorExecutor> _executors;
    private readonly IReadOnlyDictionary<OperatorType, Acme.Product.Core.Services.OperatorMetadata> _metadata;

    public OperatorBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _factory = new OperatorFactory();
        _executors = CreateExecutors();
        _metadata = _factory
            .GetAllMetadata()
            .GroupBy(item => item.Type)
            .ToDictionary(group => group.Key, group => group.First());
    }

    [Fact]
    public async Task Benchmark_CoreOperators_ShouldGenerateBaselineReport()
    {
        var entries = new List<BenchmarkEntry>();

        foreach (var type in BenchmarkTypes)
        {
            if (!_executors.TryGetValue(type, out var executor))
            {
                _output.WriteLine($"[Skip] {type}: executor not found.");
                continue;
            }

            var metadata = _metadata.GetValueOrDefault(type);

            foreach (var (width, height) in BenchmarkSizes)
            {
                var samples = await RunBenchmarkAsync(type, executor, metadata, width, height);
                if (samples.Count == 0)
                {
                    continue;
                }

                var avg = samples.Average();
                var p95 = Percentile(samples, 0.95);
                var p99 = Percentile(samples, 0.99);
                var needsOptimization = avg > 100;

                var entry = new BenchmarkEntry(
                    type,
                    width,
                    height,
                    samples.Count,
                    avg,
                    p95,
                    p99,
                    needsOptimization);

                entries.Add(entry);
                _output.WriteLine(
                    $"{type} @ {width}x{height}: avg={avg:F2}ms, p95={p95:F2}ms, p99={p99:F2}ms, status={(needsOptimization ? "NeedOptimize" : "OK")}");
            }
        }

        Assert.NotEmpty(entries);

        var reportPath = WriteReport(entries);
        _output.WriteLine($"Benchmark report written: {reportPath}");
    }

    private async Task<List<long>> RunBenchmarkAsync(
        OperatorType type,
        IOperatorExecutor executor,
        Acme.Product.Core.Services.OperatorMetadata? metadata,
        int width,
        int height)
    {
        var iterations = width >= 4000 ? 5 : 8;
        var samples = new List<long>(iterations);

        using var baseImage = CreateBenchmarkImage(width, height);

        _ = await ExecuteOnceAsync(type, executor, metadata, baseImage);

        for (var i = 0; i < iterations; i++)
        {
            var elapsed = await ExecuteOnceAsync(type, executor, metadata, baseImage);
            if (elapsed >= 0)
            {
                samples.Add(elapsed);
            }
        }

        return samples;
    }

    private async Task<long> ExecuteOnceAsync(
        OperatorType type,
        IOperatorExecutor executor,
        Acme.Product.Core.Services.OperatorMetadata? metadata,
        Mat baseImage)
    {
        var op = _factory.CreateOperator(type, $"{type}_Benchmark", 0, 0);
        var inputs = BuildInputs(metadata, baseImage);

        OperatorExecutionOutput? result = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result = await executor.ExecuteAsync(op, inputs);
            stopwatch.Stop();

            if (!result.IsSuccess)
            {
                _output.WriteLine($"[Skip sample] {type}: {result.ErrorMessage}");
                return -1;
            }

            return stopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _output.WriteLine($"[Skip sample] {type}: threw {ex.GetType().Name} - {ex.Message}");
            return -1;
        }
        finally
        {
            DisposeObjectGraph(result?.OutputData);
            DisposeObjectGraph(inputs);
        }
    }

    private static Dictionary<string, object> BuildInputs(Acme.Product.Core.Services.OperatorMetadata? metadata, Mat baseImage)
    {
        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var ports = metadata?.InputPorts ?? [];
        if (ports.Count == 0)
        {
            inputs["Image"] = new ImageWrapper(baseImage.Clone());
            return inputs;
        }

        foreach (var port in ports)
        {
            inputs[port.Name] = CreateSampleValue(port.DataType, baseImage);
        }

        return inputs;
    }

    private static object CreateSampleValue(PortDataType dataType, Mat baseImage)
    {
        return dataType switch
        {
            PortDataType.Image => new ImageWrapper(baseImage.Clone()),
            PortDataType.Integer => 1,
            PortDataType.Float => 1.0,
            PortDataType.Boolean => true,
            PortDataType.String => "benchmark",
            PortDataType.Point => new Position(10, 10),
            PortDataType.Rectangle => new RegionOfInterest("roi", 0, 0, 128, 128),
            PortDataType.Contour => Array.Empty<CvPoint>(),
            PortDataType.PointList => new List<Position> { new(10, 10), new(20, 20), new(30, 30) },
            PortDataType.DetectionResult => new DetectionResult("sample", 0.9f, 10, 10, 20, 20),
            PortDataType.DetectionList => new DetectionList(new[]
            {
                new DetectionResult("sample", 0.9f, 10, 10, 20, 20)
            }),
            PortDataType.CircleData => new CircleData(50, 50, 10),
            PortDataType.LineData => new LineData(0, 0, 100, 0),
            _ => 1
        };
    }

    private static Mat CreateBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(image, new Rect(width / 8, height / 8, width / 3, height / 4), Scalar.White, -1);
        Cv2.Circle(image, new CvPoint(width / 2, height / 2), Math.Max(20, Math.Min(width, height) / 8), new Scalar(0, 0, 255), -1);
        Cv2.Line(image, new CvPoint(width / 4, height * 3 / 4), new CvPoint(width * 3 / 4, height / 4), new Scalar(0, 255, 0), 3);
        return image;
    }

    private static double Percentile(IReadOnlyList<long> values, double percentile)
    {
        if (values.Count == 0)
            return 0;

        var ordered = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static string WriteReport(IReadOnlyList<BenchmarkEntry> entries)
    {
        var repoRoot = ResolveAcmeProductRoot();
        var reportDirectory = Path.Combine(repoRoot, "test_results");

        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, "operator_benchmark_report.md");
        var builder = new StringBuilder();

        builder.AppendLine("# Operator Benchmark Report");
        builder.AppendLine();
        builder.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---|");

        foreach (var entry in entries.OrderBy(item => item.AverageMs))
        {
            builder.AppendLine(
                $"| {entry.OperatorType} | {entry.Width}x{entry.Height} | {entry.Iterations} | {entry.AverageMs:F2} | {entry.P95Ms:F2} | {entry.P99Ms:F2} | {(entry.NeedsOptimization ? "NeedOptimize" : "OK")} |");
        }

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    private static string ResolveAcmeProductRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var srcPath = Path.Combine(current.FullName, "src");
            var testsPath = Path.Combine(current.FullName, "tests");
            if (Directory.Exists(srcPath) && Directory.Exists(testsPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record BenchmarkEntry(
        OperatorType OperatorType,
        int Width,
        int Height,
        int Iterations,
        double AverageMs,
        double P95Ms,
        double P99Ms,
        bool NeedsOptimization);

    private static IReadOnlyDictionary<OperatorType, IOperatorExecutor> CreateExecutors()
    {
        return new Dictionary<OperatorType, IOperatorExecutor>
        {
            [OperatorType.Filtering] = new GaussianBlurOperator(NullLogger<GaussianBlurOperator>.Instance),
            [OperatorType.Thresholding] = new ThresholdOperator(NullLogger<ThresholdOperator>.Instance),
            [OperatorType.EdgeDetection] = new CannyEdgeOperator(NullLogger<CannyEdgeOperator>.Instance),
            [OperatorType.Morphology] = new MorphologyOperator(NullLogger<MorphologyOperator>.Instance),
            [OperatorType.BlobAnalysis] = new BlobDetectionOperator(NullLogger<BlobDetectionOperator>.Instance),
            [OperatorType.SharpnessEvaluation] = new SharpnessEvaluationOperator(NullLogger<SharpnessEvaluationOperator>.Instance)
        };
    }

    private static void DisposeObjectGraph(object? value)
    {
        if (value == null)
            return;

        if (value is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                DisposeObjectGraph(entry.Value);
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                DisposeObjectGraph(item);
            }
        }
    }
}
