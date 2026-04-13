using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Integration;

[Collection(PerformanceAcceptanceCollection.Name)]
public sealed class DetectionPerformanceBudgetAcceptanceTests
{
    private const double DefaultBudgetScale = 1.5;
    private const string ReportFileStem = "detection_performance_budget_report";

    [Fact(Timeout = 300000)]
    public async Task W5_DetectionOperatorPerformanceBudget_512_ShouldMeetUnifiedGate()
    {
        var warmupIterations = GetEnvInt("CV_DETECTION_PERF_WARMUP_ITERS", 5, 0, 100);
        var measuredIterations = GetEnvInt("CV_DETECTION_PERF_MEASURE_ITERS", 24, 10, 400);
        var budgetScale = GetEnvDouble("CV_DETECTION_PERF_BUDGET_SCALE", DefaultBudgetScale, 0.5, 10.0);
        var gateProfile = GetEnvString("CV_DETECTION_PERF_GATE_PROFILE", "standard");

        var angle = new AngleMeasurementOperator(NullLogger<AngleMeasurementOperator>.Instance);
        var caliper = new CaliperToolOperator(NullLogger<CaliperToolOperator>.Instance);
        var circle = new CircleMeasurementOperator(NullLogger<CircleMeasurementOperator>.Instance);
        var contour = new ContourMeasurementOperator(NullLogger<ContourMeasurementOperator>.Instance);
        var gap = new GapMeasurementOperator(NullLogger<GapMeasurementOperator>.Instance);
        var geo = new GeoMeasurementOperator(NullLogger<GeoMeasurementOperator>.Instance);
        var tolerance = new GeometricToleranceOperator(NullLogger<GeometricToleranceOperator>.Instance);
        var histogram = new HistogramAnalysisOperator(NullLogger<HistogramAnalysisOperator>.Instance);
        var lineLineDistance = new LineLineDistanceOperator(NullLogger<LineLineDistanceOperator>.Instance);
        var line = new LineMeasurementOperator(NullLogger<LineMeasurementOperator>.Instance);
        var measureDistance = new MeasureDistanceOperator(NullLogger<MeasureDistanceOperator>.Instance);
        var pixelStats = new PixelStatisticsOperator(NullLogger<PixelStatisticsOperator>.Instance);
        var pointLineDistance = new PointLineDistanceOperator(NullLogger<PointLineDistanceOperator>.Instance);
        var sharpness = new SharpnessEvaluationOperator(NullLogger<SharpnessEvaluationOperator>.Instance);
        var width = new WidthMeasurementOperator(NullLogger<WidthMeasurementOperator>.Instance);

        var angleOp = new Operator("AngleMeasurement", OperatorType.AngleMeasurement, 0, 0);

        var caliperOp = new Operator("CaliperTool", OperatorType.CaliperTool, 0, 0);
        AddParam(caliperOp, "Direction", "Horizontal", "string");
        AddParam(caliperOp, "Polarity", "Both", "string");
        AddParam(caliperOp, "EdgeThreshold", 18.0, "double");
        AddParam(caliperOp, "ExpectedCount", 1, "int");
        AddParam(caliperOp, "MeasureMode", "edge_pairs", "string");
        AddParam(caliperOp, "SubpixelAccuracy", false, "bool");

        var circleOp = new Operator("CircleMeasurement", OperatorType.CircleMeasurement, 0, 0);
        AddParam(circleOp, "Method", "HoughCircle", "string");
        AddParam(circleOp, "MinRadius", 30, "int");
        AddParam(circleOp, "MaxRadius", 140, "int");

        var contourOp = new Operator("ContourMeasurement", OperatorType.ContourMeasurement, 0, 0);
        AddParam(contourOp, "Threshold", 100.0, "double");
        AddParam(contourOp, "MinArea", 50, "int");
        AddParam(contourOp, "MaxArea", 200000, "int");

        var gapOp = new Operator("GapMeasurement", OperatorType.GapMeasurement, 0, 0);
        AddParam(gapOp, "Direction", "Horizontal", "string");
        AddParam(gapOp, "ExpectedCount", 4, "int");
        AddParam(gapOp, "RobustMode", true, "bool");
        AddParam(gapOp, "OutlierSigmaK", 3.0, "double");
        AddParam(gapOp, "MultiScanCount", 8, "int");

        var geoOp = new Operator("GeoMeasurement", OperatorType.GeoMeasurement, 0, 0);
        AddParam(geoOp, "Element1Type", "Line", "string");
        AddParam(geoOp, "Element2Type", "Line", "string");
        AddParam(geoOp, "DistanceModel", "InfiniteLine", "string");

        var toleranceOp = new Operator("GeometricTolerance", OperatorType.GeometricTolerance, 0, 0);
        AddParam(toleranceOp, "ToleranceType", "Parallelism", "string");
        AddParam(toleranceOp, "ZoneSize", 4.0, "double");

        AssertGeometricToleranceBudgetContractAlignment(toleranceOp);

        var histogramOp = new Operator("HistogramAnalysis", OperatorType.HistogramAnalysis, 0, 0);
        AddParam(histogramOp, "Channel", "Gray", "string");
        AddParam(histogramOp, "BinCount", 128, "int");

        var lineLineOp = new Operator("LineLineDistance", OperatorType.LineLineDistance, 0, 0);
        AddParam(lineLineOp, "ParallelThreshold", 2.0, "double");

        var lineOp = new Operator("LineMeasurement", OperatorType.LineMeasurement, 0, 0);
        AddParam(lineOp, "Method", "FitLine", "string");
        AddParam(lineOp, "Threshold", 80, "int");
        AddParam(lineOp, "MinLength", 80.0, "double");
        AddParam(lineOp, "MaxGap", 10.0, "double");

        var measureDistanceOp = new Operator("MeasureDistance", OperatorType.Measurement, 0, 0);
        AddParam(measureDistanceOp, "MeasureType", "PointToPoint", "string");
        AddParam(measureDistanceOp, "X1", 80, "int");
        AddParam(measureDistanceOp, "Y1", 110, "int");
        AddParam(measureDistanceOp, "X2", 420, "int");
        AddParam(measureDistanceOp, "Y2", 360, "int");

        var pixelStatsOp = new Operator("PixelStatistics", OperatorType.PixelStatistics, 0, 0);
        AddParam(pixelStatsOp, "Channel", "Gray", "string");

        var pointLineOp = new Operator("PointLineDistance", OperatorType.PointLineDistance, 0, 0);

        var sharpnessOp = new Operator("SharpnessEvaluation", OperatorType.SharpnessEvaluation, 0, 0);
        AddParam(sharpnessOp, "Method", "Laplacian", "string");
        AddParam(sharpnessOp, "Threshold", 0.0, "double");

        var widthOp = new Operator("WidthMeasurement", OperatorType.WidthMeasurement, 0, 0);
        AddParam(widthOp, "MeasureMode", "ManualLines", "string");
        AddParam(widthOp, "MultiScanCount", 24, "int");
        AddParam(widthOp, "RobustMode", true, "bool");
        AddParam(widthOp, "OutlierSigmaK", 3.0, "double");

        var cases = new List<DetectionBudgetCase>
        {
            new("AngleMeasurement", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("AngleMeasurement", angle, angleOp, inputs);
            }),
            new("CaliperTool", 50.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateCaliperImage(512, 512) };
                await ExecuteCaseAsync("CaliperTool", caliper, caliperOp, inputs);
            }),
            new("CircleMeasurement", 30.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateCircleImage(512, 512) };
                await ExecuteCaseAsync("CircleMeasurement", circle, circleOp, inputs);
            }),
            new("ContourMeasurement", 40.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("ContourMeasurement", contour, contourOp, inputs);
            }),
            new("GapMeasurement", 30.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateGapImage(512, 512) };
                await ExecuteCaseAsync("GapMeasurement", gap, gapOp, inputs);
            }),
            new("GeoMeasurement", 20.0, async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Element1"] = new LineData(60, 200, 460, 200),
                    ["Element2"] = new LineData(260, 40, 260, 460)
                };
                await ExecuteCaseAsync("GeoMeasurement", geo, geoOp, inputs);
            }),
            new("GeometricTolerance", 20.0, async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Image"] = CreateMeasurementImage(512, 512),
                    ["FeaturePrimary"] = new LineData(60, 120, 460, 140),
                    ["DatumA"] = new LineData(60, 220, 460, 240)
                };
                await ExecuteCaseAsync("GeometricTolerance", tolerance, toleranceOp, inputs);
            }),
            new("HistogramAnalysis", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("HistogramAnalysis", histogram, histogramOp, inputs);
            }),
            new("LineLineDistance", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Line1"] = new LineData(20, 20, 420, 20),
                    ["Line2"] = new LineData(20, 160, 420, 160)
                };
                await ExecuteCaseAsync("LineLineDistance", lineLineDistance, lineLineOp, inputs);
            }),
            new("LineMeasurement", 20.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateLineImage(512, 512) };
                await ExecuteCaseAsync("LineMeasurement", line, lineOp, inputs);
            }),
            new("MeasureDistance", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("MeasureDistance", measureDistance, measureDistanceOp, inputs);
            }),
            new("PixelStatistics", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("PixelStatistics", pixelStats, pixelStatsOp, inputs);
            }),
            new("PointLineDistance", 10.0, async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Point"] = new Position(240, 210),
                    ["Line"] = new LineData(60, 200, 460, 200)
                };
                await ExecuteCaseAsync("PointLineDistance", pointLineDistance, pointLineOp, inputs);
            }),
            new("SharpnessEvaluation", 15.0, async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = CreateMeasurementImage(512, 512) };
                await ExecuteCaseAsync("SharpnessEvaluation", sharpness, sharpnessOp, inputs);
            }),
            new("WidthMeasurement", 30.0, async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Image"] = CreateCaliperImage(512, 512),
                    ["Line1"] = new LineData(180, 120, 180, 390),
                    ["Line2"] = new LineData(300, 120, 300, 390)
                };
                await ExecuteCaseAsync("WidthMeasurement", width, widthOp, inputs);
            })
        };

        var entries = new List<PerformanceEntry>(cases.Count);

        foreach (var testCase in cases)
        {
            var allowed = testCase.BudgetMs * budgetScale;

            try
            {
                var stats = await MeasureAsync(testCase.ExecuteAsync, warmupIterations, measuredIterations);
                var status = stats.P95Ms <= allowed ? "PASS" : "FAIL";
                var notes = status == "PASS"
                    ? "Within budget."
                    : $"p95 {stats.P95Ms:F2}ms exceeded allowed {allowed:F2}ms.";

                entries.Add(new PerformanceEntry(
                    testCase.Name,
                    testCase.BudgetMs,
                    budgetScale,
                    allowed,
                    stats.MeanMs,
                    stats.P95Ms,
                    stats.P99Ms,
                    status,
                    notes));
            }
            catch (Exception ex)
            {
                entries.Add(new PerformanceEntry(
                    testCase.Name,
                    testCase.BudgetMs,
                    budgetScale,
                    allowed,
                    0.0,
                    0.0,
                    0.0,
                    "ERROR",
                    $"Execution failed: {ex.Message}"));
            }
        }

        var reportArtifacts = WriteReport(entries, warmupIterations, measuredIterations, budgetScale, gateProfile);
        Console.WriteLine($"Detection performance budget report written: {reportArtifacts.MarkdownPath}");

        var failed = entries.Where(entry => !entry.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase)).ToList();
        if (failed.Count > 0)
        {
            var archivePath = ArchiveFailureReport(
                reportArtifacts,
                failed,
                warmupIterations,
                measuredIterations,
                budgetScale,
                gateProfile);
            Console.WriteLine($"Detection performance failure archive written: {archivePath}");
        }

        Assert.True(
            failed.Count == 0,
            "Detection performance budget gate failed: " +
            string.Join("; ", failed.Select(item => $"{item.Name}({item.Status}): {item.Notes}")));
    }

    private static async Task ExecuteCaseAsync(string name, OperatorBase executor, Operator op, Dictionary<string, object> inputs)
    {
        OperatorExecutionOutput? result = null;
        RetainInputImageWrappers(inputs);

        try
        {
            result = await executor.ExecuteAsync(op, inputs);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"{name} failed: {result.ErrorMessage}");
            }
        }
        finally
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            DisposeObjectGraph(result?.OutputData, visited);
            DisposeObjectGraph(inputs, visited);
        }
    }

    private static void RetainInputImageWrappers(Dictionary<string, object>? inputs)
    {
        if (inputs == null)
        {
            return;
        }

        var retained = new HashSet<ImageWrapper>(ReferenceEqualityComparer.Instance);
        foreach (var value in inputs.Values)
        {
            if (value is ImageWrapper image && retained.Add(image))
            {
                image.AddRef();
            }
        }
    }

    private static async Task<PerfStats> MeasureAsync(Func<Task> action, int warmupIterations, int measuredIterations)
    {
        for (var i = 0; i < warmupIterations; i++)
        {
            await action();
            await Task.Yield();
        }

        var samples = new List<double>(measuredIterations);
        for (var i = 0; i < measuredIterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            await action();
            var end = Stopwatch.GetTimestamp();
            samples.Add((end - start) * 1000.0 / Stopwatch.Frequency);
        }

        samples.Sort();
        return new PerfStats(samples);
    }

    private static PerformanceReportArtifacts WriteReport(
        IReadOnlyList<PerformanceEntry> entries,
        int warmupIterations,
        int measuredIterations,
        double budgetScale,
        string gateProfile)
    {
        var repoRoot = ResolveAcmeProductRoot();
        var reportDir = Path.Combine(repoRoot, "test_results");
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, $"{ReportFileStem}.md");
        var jsonPath = Path.Combine(reportDir, $"{ReportFileStem}.json");
        var generatedAtUtc = DateTime.UtcNow;
        var builder = new StringBuilder();
        builder.AppendLine("# Detection Performance Budget Report");
        builder.AppendLine();
        builder.AppendLine($"Generated (UTC): {generatedAtUtc:O}");
        builder.AppendLine($"Gate Profile: {gateProfile}");
        builder.AppendLine($"Warmup Iterations: {warmupIterations}");
        builder.AppendLine($"Measured Iterations: {measuredIterations}");
        builder.AppendLine($"Budget Scale: {budgetScale.ToString("0.00", CultureInfo.InvariantCulture)}");
        builder.AppendLine();
        builder.AppendLine("| Operator | Budget (ms) | Scale | Allowed P95 (ms) | Mean (ms) | P95 (ms) | P99 (ms) | Status | Notes |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|---|");

        foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                $"| {entry.Name} | {entry.BudgetMs.ToString("0.##", CultureInfo.InvariantCulture)} | {entry.Scale.ToString("0.00", CultureInfo.InvariantCulture)} | {entry.AllowedMs.ToString("0.##", CultureInfo.InvariantCulture)} | {entry.MeanMs.ToString("0.00", CultureInfo.InvariantCulture)} | {entry.P95Ms.ToString("0.00", CultureInfo.InvariantCulture)} | {entry.P99Ms.ToString("0.00", CultureInfo.InvariantCulture)} | {entry.Status} | {entry.Notes} |");
        }

        File.WriteAllText(reportPath, builder.ToString());
        var json = JsonSerializer.Serialize(
            new
            {
                GeneratedAtUtc = generatedAtUtc,
                GateProfile = gateProfile,
                WarmupIterations = warmupIterations,
                MeasuredIterations = measuredIterations,
                BudgetScale = budgetScale,
                Entries = entries
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        return new PerformanceReportArtifacts(reportDir, reportPath, jsonPath, generatedAtUtc);
    }

    private static string ArchiveFailureReport(
        PerformanceReportArtifacts artifacts,
        IReadOnlyList<PerformanceEntry> failedEntries,
        int warmupIterations,
        int measuredIterations,
        double budgetScale,
        string gateProfile)
    {
        var archiveRoot = GetEnvString(
            "CV_DETECTION_PERF_FAILURE_ARCHIVE_DIR",
            Path.Combine(artifacts.ReportDirectory, "archive", "detection_performance_failures"));
        Directory.CreateDirectory(archiveRoot);

        var runId = string.Concat(
            artifacts.GeneratedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture),
            "_",
            SanitizeForPath(gateProfile),
            "_s",
            budgetScale.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', '_'));
        var archiveDir = Path.Combine(archiveRoot, runId);
        Directory.CreateDirectory(archiveDir);

        var archiveReportPath = Path.Combine(archiveDir, Path.GetFileName(artifacts.MarkdownPath));
        var archiveJsonPath = Path.Combine(archiveDir, Path.GetFileName(artifacts.JsonPath));
        File.Copy(artifacts.MarkdownPath, archiveReportPath, overwrite: true);
        File.Copy(artifacts.JsonPath, archiveJsonPath, overwrite: true);

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine("# Detection Performance Budget Failure Summary");
        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine($"Generated (UTC): {artifacts.GeneratedAtUtc:O}");
        summaryBuilder.AppendLine($"Gate Profile: {gateProfile}");
        summaryBuilder.AppendLine($"Warmup Iterations: {warmupIterations}");
        summaryBuilder.AppendLine($"Measured Iterations: {measuredIterations}");
        summaryBuilder.AppendLine($"Budget Scale: {budgetScale.ToString("0.00", CultureInfo.InvariantCulture)}");
        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine("## Failed Entries");
        summaryBuilder.AppendLine();

        foreach (var failed in failedEntries.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            summaryBuilder.AppendLine(
                $"- {failed.Name}: Status={failed.Status}, Budget={failed.BudgetMs.ToString("0.##", CultureInfo.InvariantCulture)}ms, AllowedP95={failed.AllowedMs.ToString("0.##", CultureInfo.InvariantCulture)}ms, P95={failed.P95Ms.ToString("0.00", CultureInfo.InvariantCulture)}ms, Notes={failed.Notes}");
        }

        File.WriteAllText(Path.Combine(archiveDir, "failure_summary.md"), summaryBuilder.ToString());

        var manifest = new
        {
            GeneratedAtUtc = artifacts.GeneratedAtUtc,
            ArchivedAtUtc = DateTime.UtcNow,
            GateProfile = gateProfile,
            WarmupIterations = warmupIterations,
            MeasuredIterations = measuredIterations,
            BudgetScale = budgetScale,
            ArchiveDirectory = archiveDir,
            MachineName = Environment.MachineName,
            RuntimeVersion = Environment.Version.ToString(),
            FailedEntries = failedEntries
        };
        File.WriteAllText(
            Path.Combine(archiveDir, "failure_manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return archiveDir;
    }

    private static string SanitizeForPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
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

    private static int GetEnvInt(string name, int defaultValue, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static double GetEnvDouble(string name, double defaultValue, double min, double max)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static string GetEnvString(string name, string defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : raw.Trim();
    }

    private static void AssertGeometricToleranceBudgetContractAlignment(Operator toleranceOp)
    {
        var inputPorts = typeof(GeometricToleranceOperator)
            .GetCustomAttributes<InputPortAttribute>(inherit: false)
            .ToArray();

        Assert.True(
            HasRequiredInputPort(inputPorts, "FeaturePrimary"),
            "Contract drift detected: GeometricToleranceOperator must expose required input port 'FeaturePrimary'. " +
            "Update DetectionPerformanceBudgetAcceptanceTests GeometricTolerance fixture.");
        Assert.True(
            HasRequiredInputPort(inputPorts, "DatumA"),
            "Contract drift detected: GeometricToleranceOperator must expose required input port 'DatumA'. " +
            "Update DetectionPerformanceBudgetAcceptanceTests GeometricTolerance fixture.");
        Assert.True(
            HasOptionalInputPort(inputPorts, "DatumB"),
            "Contract drift detected: GeometricToleranceOperator must expose optional input port 'DatumB'. " +
            "Update DetectionPerformanceBudgetAcceptanceTests GeometricTolerance fixture.");

        var paramNames = toleranceOp.Parameters
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            paramNames.Contains("ToleranceType"),
            "Contract drift detected: GeometricTolerance budget fixture must configure parameter 'ToleranceType'.");
        Assert.DoesNotContain(
            "MeasureType",
            paramNames);
    }

    private static bool HasRequiredInputPort(IEnumerable<InputPortAttribute> inputPorts, string portName)
    {
        return inputPorts.Any(port =>
            port.Name.Equals(portName, StringComparison.OrdinalIgnoreCase) &&
            port.IsRequired);
    }

    private static bool HasOptionalInputPort(IEnumerable<InputPortAttribute> inputPorts, string portName)
    {
        return inputPorts.Any(port =>
            port.Name.Equals(portName, StringComparison.OrdinalIgnoreCase) &&
            !port.IsRequired);
    }

    private static void AddParam(Operator op, string name, object value, string dataType)
    {
        op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, dataType, value));
    }

    private static ImageWrapper CreateMeasurementImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 50, width / 3, height / 4), Scalar.White, thickness: -1);
        Cv2.Rectangle(mat, new Rect(width / 2, height / 2, width / 3 - 20, height / 3 - 20), new Scalar(180, 180, 180), thickness: -1);
        Cv2.Circle(mat, new Point(width / 2, height / 3), 70, new Scalar(255, 255, 255), 2);
        Cv2.Line(mat, new Point(30, height - 40), new Point(width - 30, 40), new Scalar(0, 255, 0), 3);
        Cv2.Line(mat, new Point(30, 40), new Point(width - 30, height - 40), new Scalar(0, 255, 255), 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateCircleImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Circle(mat, new Point(width / 2, height / 2), 90, Scalar.White, 3);
        Cv2.Circle(mat, new Point(width / 2, height / 2), 45, Scalar.White, 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateLineImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Line(mat, new Point(40, 80), new Point(width - 40, 100), Scalar.White, 2);
        Cv2.Line(mat, new Point(70, height / 2), new Point(width - 70, height / 2), Scalar.White, 3);
        Cv2.Line(mat, new Point(100, height - 90), new Point(width - 100, height - 130), Scalar.White, 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateCaliperImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(width / 2 - 60, height / 4, 120, height / 2), Scalar.White, thickness: -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateGapImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        var xPositions = new[] { 80, 160, 240, 320, 400 };
        foreach (var x in xPositions)
        {
            Cv2.Line(mat, new Point(x, 30), new Point(x, height - 30), Scalar.White, 2);
        }

        return new ImageWrapper(mat);
    }

    private static void DisposeObjectGraph(object? value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        DisposeObjectGraph(value, visited);
    }

    private static void DisposeObjectGraph(object? value, HashSet<object> visited)
    {
        if (value == null)
        {
            return;
        }

        if (value is not ValueType && value is not string && !visited.Add(value))
        {
            return;
        }

        if (value is ImageWrapper imageWrapper)
        {
            imageWrapper.Release();
            return;
        }

        if (value is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                DisposeObjectGraph(entry.Value, visited);
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                DisposeObjectGraph(item, visited);
            }
        }
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0.0;
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
        index = Math.Clamp(index, 0, orderedValues.Count - 1);
        return orderedValues[index];
    }

    private sealed record DetectionBudgetCase(string Name, double BudgetMs, Func<Task> ExecuteAsync);

    private sealed record PerfStats(IReadOnlyList<double> Samples)
    {
        public double MeanMs => Samples.Count == 0 ? 0 : Samples.Average();
        public double P95Ms => Percentile(Samples, 0.95);
        public double P99Ms => Percentile(Samples, 0.99);
    }

    private sealed record PerformanceEntry(
        string Name,
        double BudgetMs,
        double Scale,
        double AllowedMs,
        double MeanMs,
        double P95Ms,
        double P99Ms,
        string Status,
        string Notes);

    private sealed record PerformanceReportArtifacts(
        string ReportDirectory,
        string MarkdownPath,
        string JsonPath,
        DateTime GeneratedAtUtc);
}
