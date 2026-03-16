using System.Diagnostics;
using System.Globalization;
using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

var options = MemoryLeakOptions.Parse(args);
var repoRoot = ResolveRepoRoot();
var imagePath = options.ImagePath ?? Path.Combine(repoRoot, "Acme.Product", "tests", "TestData", "shapes_composite.png");

if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Input image not found: {imagePath}");
    return 2;
}

var imageBytes = File.ReadAllBytes(imagePath);

var factory = new OperatorFactory();
var op = factory.CreateOperator(OperatorType.EdgeDetection, "MemoryLeakProbe", 0, 0);

var flow = new OperatorFlow("MemoryLeakFlow");
flow.AddOperator(op);

var executors = new List<IOperatorExecutor>
{
    new CannyEdgeOperator(NullLogger<CannyEdgeOperator>.Instance)
};

using var service = new FlowExecutionService(executors, NullLogger<FlowExecutionService>.Instance, new VariableContext());

var inputData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
{
    ["Image"] = imageBytes
};

GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
GC.WaitForPendingFinalizers();

var initialMemory = GC.GetTotalMemory(true);
var leakStore = new List<Mat>();
var failures = 0;

var stopwatch = Stopwatch.StartNew();

for (var i = 0; i < options.Iterations; i++)
{
    var result = await service.ExecuteFlowAsync(flow, inputData);
    if (!result.IsSuccess)
    {
        failures++;
    }

    if (options.Mode == LeakMode.Leak && i % options.LeakEvery == 0)
    {
        leakStore.Add(CreateLeakedMat(imageBytes, options.LeakSize));
    }

    if (i % 100 == 0)
    {
        await Task.Yield();
    }
}

stopwatch.Stop();

var finalMemory = GC.GetTotalMemory(true);
var deltaMb = (finalMemory - initialMemory) / (1024.0 * 1024.0);
var passes = options.Mode == LeakMode.Leak
    ? deltaMb >= options.ThresholdMb
    : deltaMb < options.ThresholdMb;

Console.WriteLine($"Mode: {options.Mode}");
Console.WriteLine($"Iterations: {options.Iterations}");
Console.WriteLine($"Failures: {failures}");
Console.WriteLine($"InitialMemoryMB: {initialMemory / (1024.0 * 1024.0):F2}");
Console.WriteLine($"FinalMemoryMB: {finalMemory / (1024.0 * 1024.0):F2}");
Console.WriteLine($"DeltaMB: {deltaMb:F2}");
Console.WriteLine($"ThresholdMB: {options.ThresholdMb:F2}");
Console.WriteLine($"Result: {(passes ? "PASS" : "FAIL")}");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s");

if (!string.IsNullOrWhiteSpace(options.ReportPath))
{
    WriteReport(options, imagePath, initialMemory, finalMemory, deltaMb, failures, passes, stopwatch.Elapsed);
    Console.WriteLine($"Report written: {options.ReportPath}");
}

return passes ? 0 : 1;

static Mat CreateLeakedMat(byte[] imageBytes, int? leakSize)
{
    if (leakSize.HasValue && leakSize.Value > 0)
    {
        return new Mat(leakSize.Value, leakSize.Value, MatType.CV_8UC3, Scalar.Black);
    }

    return Cv2.ImDecode(imageBytes, ImreadModes.Color);
}

static void WriteReport(
    MemoryLeakOptions options,
    string imagePath,
    long initialMemory,
    long finalMemory,
    double deltaMb,
    int failures,
    bool passes,
    TimeSpan elapsed)
{
    var reportPath = Path.GetFullPath(options.ReportPath!);
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");

    var culture = CultureInfo.InvariantCulture;
    var builder = new StringBuilder();
    builder.AppendLine("# Memory Leak Test Report");
    builder.AppendLine();
    builder.AppendLine($"Date (UTC): {DateTime.UtcNow:O}");
    builder.AppendLine($"Mode: {options.Mode}");
    builder.AppendLine($"Iterations: {options.Iterations}");
    builder.AppendLine($"LeakEvery: {options.LeakEvery}");
    builder.AppendLine($"LeakSize: {(options.LeakSize.HasValue ? options.LeakSize.Value.ToString(culture) : "source-image")}");
    builder.AppendLine($"ThresholdMB: {options.ThresholdMb.ToString("F2", culture)}");
    builder.AppendLine($"InputImage: {imagePath}");
    builder.AppendLine();
    builder.AppendLine("## Memory");
    builder.AppendLine();
    builder.AppendLine($"- InitialMB: {(initialMemory / (1024.0 * 1024.0)).ToString("F2", culture)}");
    builder.AppendLine($"- FinalMB: {(finalMemory / (1024.0 * 1024.0)).ToString("F2", culture)}");
    builder.AppendLine($"- DeltaMB: {deltaMb.ToString("F2", culture)}");
    builder.AppendLine();
    builder.AppendLine("## Result");
    builder.AppendLine();
    builder.AppendLine($"- Pass: {(passes ? "Yes" : "No")}");
    builder.AppendLine($"- Failures: {failures}");
    builder.AppendLine($"- Elapsed: {elapsed.TotalSeconds.ToString("F2", culture)}s");
    builder.AppendLine();
    builder.AppendLine("## Notes");
    builder.AppendLine();
    builder.AppendLine("- ");

    File.WriteAllText(reportPath, builder.ToString(), new UTF8Encoding(false));
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

enum LeakMode
{
    Normal,
    Leak
}

sealed record MemoryLeakOptions(
    LeakMode Mode,
    int Iterations,
    double ThresholdMb,
    int LeakEvery,
    int? LeakSize,
    string? ImagePath,
    string? ReportPath)
{
    public static MemoryLeakOptions Parse(string[] args)
    {
        var mode = LeakMode.Normal;
        var iterations = 1000;
        var thresholdMb = 50.0;
        var leakEvery = 5;
        int? leakSize = null;
        string? imagePath = null;
        string? reportPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode" when i + 1 < args.Length:
                    mode = ParseMode(args[++i]);
                    break;
                case "--iterations" when i + 1 < args.Length:
                    iterations = int.TryParse(args[++i], out var parsedIterations) ? parsedIterations : iterations;
                    break;
                case "--threshold-mb" when i + 1 < args.Length:
                    thresholdMb = double.TryParse(args[++i], out var parsedThreshold) ? parsedThreshold : thresholdMb;
                    break;
                case "--leak-every" when i + 1 < args.Length:
                    leakEvery = int.TryParse(args[++i], out var parsedLeakEvery) ? parsedLeakEvery : leakEvery;
                    break;
                case "--leak-size" when i + 1 < args.Length:
                    leakSize = int.TryParse(args[++i], out var parsedLeakSize) ? parsedLeakSize : leakSize;
                    break;
                case "--image" when i + 1 < args.Length:
                    imagePath = args[++i];
                    break;
                case "--report" when i + 1 < args.Length:
                    reportPath = args[++i];
                    break;
            }
        }

        if (iterations <= 0)
        {
            iterations = 1000;
        }

        if (leakEvery <= 0)
        {
            leakEvery = 1;
        }

        return new MemoryLeakOptions(mode, iterations, thresholdMb, leakEvery, leakSize, imagePath, reportPath);
    }

    private static LeakMode ParseMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "leak" => LeakMode.Leak,
            _ => LeakMode.Normal
        };
    }
}
