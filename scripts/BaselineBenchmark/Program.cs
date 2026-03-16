using System.Diagnostics;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;
using DetectionList = Acme.Product.Core.ValueObjects.DetectionList;
using DetectionResult = Acme.Product.Core.ValueObjects.DetectionResult;

var options = BaselineOptions.Parse(args);
var repoRoot = ResolveRepoRoot();
var dataDirectory = options.DataDirectory ?? Path.Combine(repoRoot, "Acme.Product", "tests", "TestData");
var outputPath = options.OutputPath ?? Path.Combine(repoRoot, "docs", "reports", "baseline_performance.json");

if (!Directory.Exists(dataDirectory))
{
    Console.Error.WriteLine($"Test data directory not found: {dataDirectory}");
    return 2;
}

var images = LoadImages(dataDirectory);
if (images.Count == 0)
{
    Console.Error.WriteLine($"No PNG images found in {dataDirectory}");
    return 3;
}

var factory = new OperatorFactory();
var metadata = factory.GetAllMetadata().ToDictionary(m => m.Type);

var cases = new[]
{
    new BenchmarkCase(OperatorType.Filtering, new GaussianBlurOperator(NullLogger<GaussianBlurOperator>.Instance)),
    new BenchmarkCase(OperatorType.MedianBlur, new MedianBlurOperator(NullLogger<MedianBlurOperator>.Instance)),
    new BenchmarkCase(OperatorType.Thresholding, new ThresholdOperator(NullLogger<ThresholdOperator>.Instance)),
    new BenchmarkCase(OperatorType.AdaptiveThreshold, new AdaptiveThresholdOperator(NullLogger<AdaptiveThresholdOperator>.Instance)),
    new BenchmarkCase(OperatorType.EdgeDetection, new CannyEdgeOperator(NullLogger<CannyEdgeOperator>.Instance)),
    new BenchmarkCase(OperatorType.Morphology, new MorphologyOperator(NullLogger<MorphologyOperator>.Instance)),
    new BenchmarkCase(OperatorType.BlobAnalysis, new BlobDetectionOperator(NullLogger<BlobDetectionOperator>.Instance)),
    new BenchmarkCase(OperatorType.CircleMeasurement, new CircleMeasurementOperator(NullLogger<CircleMeasurementOperator>.Instance)),
    new BenchmarkCase(OperatorType.TemplateMatching, new TemplateMatchOperator(NullLogger<TemplateMatchOperator>.Instance)),
    new BenchmarkCase(OperatorType.HistogramEqualization, new HistogramEqualizationOperator(NullLogger<HistogramEqualizationOperator>.Instance))
};

var results = new List<OperatorBaselineResult>();

foreach (var benchmarkCase in cases)
{
    var samples = new List<double>();
    string? error = null;

    foreach (var image in images)
    {
        using var template = CreateTemplate(image.Mat);
        var op = factory.CreateOperator(benchmarkCase.OperatorType, $"{benchmarkCase.OperatorType}_Baseline", 0, 0);

        for (var i = 0; i < options.WarmupIterations; i++)
        {
            _ = await ExecuteOnceAsync(benchmarkCase.Executor, op, metadata.GetValueOrDefault(benchmarkCase.OperatorType), image.Mat, template);
        }

        for (var i = 0; i < options.IterationsPerImage; i++)
        {
            var elapsed = await ExecuteOnceAsync(benchmarkCase.Executor, op, metadata.GetValueOrDefault(benchmarkCase.OperatorType), image.Mat, template);
            if (elapsed < 0)
            {
                error ??= "Execution returned failure.";
                continue;
            }
            samples.Add(elapsed);
        }
    }

    if (samples.Count == 0)
    {
        results.Add(new OperatorBaselineResult(
            benchmarkCase.OperatorType.ToString(),
            metadata.GetValueOrDefault(benchmarkCase.OperatorType)?.Category ?? "Unknown",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            "Failed",
            error ?? "No successful samples."));
        continue;
    }

    var stats = ComputeStats(samples);
    results.Add(new OperatorBaselineResult(
        benchmarkCase.OperatorType.ToString(),
        metadata.GetValueOrDefault(benchmarkCase.OperatorType)?.Category ?? "Unknown",
        samples.Count,
        stats.Average,
        stats.Min,
        stats.Max,
        stats.StdDev,
        stats.P95,
        stats.P99,
        "OK",
        error));
}

var report = new BaselineReport(
    DateTime.UtcNow,
    Environment.MachineName,
    System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    options.IterationsPerImage,
    images.Select(i => new TestImageInfo(i.Name, i.Path, i.Mat.Width, i.Mat.Height)).ToList(),
    results);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

File.WriteAllText(outputPath, JsonSerializer.Serialize(report, jsonOptions));
Console.WriteLine($"Baseline report written: {outputPath}");

foreach (var image in images)
{
    image.Mat.Dispose();
}

return 0;

static async Task<double> ExecuteOnceAsync(
    IOperatorExecutor executor,
    Operator op,
    OperatorMetadata? metadata,
    Mat baseImage,
    Mat templateImage)
{
    var inputs = BuildInputs(metadata, baseImage, templateImage);
    var stopwatch = Stopwatch.StartNew();
    OperatorExecutionOutput? result = null;

    try
    {
        result = await executor.ExecuteAsync(op, inputs);
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            return -1;
        }

        return stopwatch.Elapsed.TotalMilliseconds;
    }
    catch
    {
        stopwatch.Stop();
        return -1;
    }
    finally
    {
        DisposeObjectGraph(result?.OutputData);
        DisposeObjectGraph(inputs);
    }
}

static Dictionary<string, object> BuildInputs(OperatorMetadata? metadata, Mat baseImage, Mat templateImage)
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
        if (port.DataType == PortDataType.Image)
        {
            if (port.Name.Contains("template", StringComparison.OrdinalIgnoreCase))
            {
                inputs[port.Name] = new ImageWrapper(templateImage.Clone());
            }
            else
            {
                inputs[port.Name] = new ImageWrapper(baseImage.Clone());
            }
            continue;
        }

        inputs[port.Name] = CreateSampleValue(port.DataType, baseImage);
    }

    return inputs;
}

static object CreateSampleValue(PortDataType dataType, Mat baseImage)
{
    return dataType switch
    {
        PortDataType.Image => new ImageWrapper(baseImage.Clone()),
        PortDataType.Integer => 1,
        PortDataType.Float => 1.0,
        PortDataType.Boolean => true,
        PortDataType.String => "baseline",
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

static void DisposeObjectGraph(object? value)
{
    if (value == null)
        return;

    if (value is IDisposable disposable)
    {
        disposable.Dispose();
        return;
    }

    if (value is System.Collections.IDictionary dictionary)
    {
        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            DisposeObjectGraph(entry.Value);
        }

        return;
    }

    if (value is System.Collections.IEnumerable enumerable && value is not string)
    {
        foreach (var item in enumerable)
        {
            DisposeObjectGraph(item);
        }
    }
}

static Mat CreateTemplate(Mat baseImage)
{
    var size = new CvSize(Math.Min(128, baseImage.Width / 2), Math.Min(128, baseImage.Height / 2));
    var rect = new CvRect(
        Math.Max(0, baseImage.Width / 2 - size.Width / 2),
        Math.Max(0, baseImage.Height / 2 - size.Height / 2),
        size.Width,
        size.Height);
    return new Mat(baseImage, rect).Clone();
}

static List<TestImage> LoadImages(string dataDirectory)
{
    var files = Directory.GetFiles(dataDirectory, "*.png", SearchOption.TopDirectoryOnly);
    var preferred = new[]
    {
        "shapes_composite.png",
        "iso12233_slant_edge_512.png"
    };

    var selected = new List<string>();
    foreach (var name in preferred)
    {
        var path = Path.Combine(dataDirectory, name);
        if (File.Exists(path))
        {
            selected.Add(path);
        }
    }

    if (selected.Count == 0)
    {
        selected.AddRange(files.Take(2));
    }

    var images = new List<TestImage>();
    foreach (var path in selected)
    {
        var mat = Cv2.ImRead(path, ImreadModes.Color);
        if (mat.Empty())
        {
            continue;
        }

        images.Add(new TestImage(Path.GetFileName(path), path, mat));
    }

    return images;
}

static Stats ComputeStats(IReadOnlyList<double> samples)
{
    var ordered = samples.OrderBy(s => s).ToArray();
    var avg = samples.Average();
    var min = ordered.First();
    var max = ordered.Last();
    var variance = samples.Sum(s => Math.Pow(s - avg, 2)) / samples.Count;
    var stdDev = Math.Sqrt(variance);
    var p95 = Percentile(ordered, 0.95);
    var p99 = Percentile(ordered, 0.99);
    return new Stats(avg, min, max, stdDev, p95, p99);
}

static double Percentile(double[] ordered, double percentile)
{
    if (ordered.Length == 0)
        return 0;

    var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
    index = Math.Clamp(index, 0, ordered.Length - 1);
    return ordered[index];
}

static string ResolveRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}

sealed record BenchmarkCase(OperatorType OperatorType, IOperatorExecutor Executor);
sealed record TestImage(string Name, string Path, Mat Mat);
sealed record Stats(double Average, double Min, double Max, double StdDev, double P95, double P99);

sealed record BaselineReport(
    DateTime GeneratedAtUtc,
    string MachineName,
    string OperatingSystem,
    string Framework,
    int IterationsPerImage,
    List<TestImageInfo> Images,
    List<OperatorBaselineResult> Operators);

sealed record TestImageInfo(string Name, string Path, int Width, int Height);

sealed record OperatorBaselineResult(
    string OperatorType,
    string Category,
    int Samples,
    double AverageMs,
    double MinMs,
    double MaxMs,
    double StdDevMs,
    double P95Ms,
    double P99Ms,
    string Status,
    string? Error);

sealed record BaselineOptions(int IterationsPerImage, int WarmupIterations, string? DataDirectory, string? OutputPath)
{
    public static BaselineOptions Parse(string[] args)
    {
        var iterations = 8;
        var warmup = 1;
        string? dataDir = null;
        string? output = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--iterations" when i + 1 < args.Length:
                    iterations = int.TryParse(args[++i], out var parsedIterations) ? parsedIterations : iterations;
                    break;
                case "--warmup" when i + 1 < args.Length:
                    warmup = int.TryParse(args[++i], out var parsedWarmup) ? parsedWarmup : warmup;
                    break;
                case "--data-dir" when i + 1 < args.Length:
                    dataDir = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
            }
        }

        return new BaselineOptions(iterations, warmup, dataDir, output);
    }
}
