using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Integration;

[Collection(PerformanceAcceptanceCollection.Name)]
public sealed class PerformanceBudgetAcceptanceTests
{
    // Budgets derived from docs/对标halcon的算子深化_0316/performance_requirements.md (V1.0, 2026-03-16).
    // We apply a scaling factor by default to keep the tests stable across developer machines.
    private const double TemplateMatchBudgetMs = 100.0;
    private const double ShapeMatchBudgetMs = 200.0;
    private const double BlobAnalysisBudgetMs = 30.0;
    private const double CaliperBudgetMs = 50.0;
    private const double UndistortBudgetMs = 10.0;
    private const double CalibrationProducerBudgetMs = 250.0;
    private const double CalibrationConsumerBudgetMs = 80.0;
    private const double CalibrationUtilityBudgetMs = 40.0;

    [Fact(Timeout = 180000)]
    public async Task W5_PerformanceBudget_KeyOperators_512_ShouldMeetBudgets()
    {
        var warmupIterations = GetEnvInt("CV_PERF_WARMUP_ITERS", 8, 0, 100);
        var measuredIterations = GetEnvInt("CV_PERF_MEASURE_ITERS", 40, 10, 400);

        // Default: allow 2x slack. Set to 1.0 for strict acceptance on your reference machine.
        var budgetScale = GetEnvDouble("CV_PERF_BUDGET_SCALE", 2.0, 0.5, 10.0);

        using var template64 = CreateStandardTemplate(size: 64);
        using var scene512 = CreateSceneByWarpingTemplate(template64.MatReadOnly, width: 512, height: 512, x: 180.25, y: 140.75);

        using var blobImage = CreateBlobBenchmarkImage(width: 512, height: 512, blobCount: 10);
        using var caliperImage = CreateCaliperBenchmarkImage(width: 512, height: 512);
        using var undistortImage = CreateUndistortBenchmarkImage(width: 512, height: 512);

        // Template matching (<100ms @ 512x512, 64x64).
        var templateMatch = new TemplateMatchOperator(NullLogger<TemplateMatchOperator>.Instance);
        var templateMatchOp = new Operator("TemplateMatch", OperatorType.TemplateMatching, 0, 0);
        templateMatchOp.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "CCoeffNormed"));
        templateMatchOp.AddParameter(new Parameter(Guid.NewGuid(), "Threshold", "Threshold", string.Empty, "double", 0.8));
        templateMatchOp.AddParameter(new Parameter(Guid.NewGuid(), "MaxMatches", "MaxMatches", string.Empty, "int", 1));

        var templateStats = await MeasureAsync(
            async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Image"] = scene512.AddRef(),
                    ["Template"] = template64.AddRef()
                };
                var result = await templateMatch.ExecuteAsync(templateMatchOp, inputs);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                DisposeObjectGraph(result.OutputData);
            },
            warmupIterations,
            measuredIterations);

        AssertBudget("TemplateMatch", TemplateMatchBudgetMs, budgetScale, templateStats);

        // Shape matching (<200ms @ 512x512, 64x64; no occlusion).
        var shapeMatch = new ShapeMatchingOperator(NullLogger<ShapeMatchingOperator>.Instance);
        var shapeOp = new Operator("ShapeMatch", OperatorType.ShapeMatching, 0, 0);
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "MinScore", "MinScore", string.Empty, "double", 0.4));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "MaxMatches", "MaxMatches", string.Empty, "int", 1));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "AngleStart", "AngleStart", string.Empty, "double", 0.0));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "AngleExtent", "AngleExtent", string.Empty, "double", 0.0));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "AngleStep", "AngleStep", string.Empty, "double", 1.0));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "ScaleMin", "ScaleMin", string.Empty, "double", 1.0));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "ScaleMax", "ScaleMax", string.Empty, "double", 1.0));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "ScaleStep", "ScaleStep", string.Empty, "double", 0.1));
        shapeOp.AddParameter(new Parameter(Guid.NewGuid(), "NumLevels", "NumLevels", string.Empty, "int", 2));

        var shapeStats = await MeasureAsync(
            async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Image"] = scene512.AddRef(),
                    ["Template"] = template64.AddRef()
                };
                var result = await shapeMatch.ExecuteAsync(shapeOp, inputs);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                DisposeObjectGraph(result.OutputData);
            },
            warmupIterations,
            measuredIterations);

        AssertBudget("ShapeMatch", ShapeMatchBudgetMs, budgetScale, shapeStats);

        // Blob analysis (<30ms @ 512x512; ~10 blobs).
        var blob = new BlobDetectionOperator(NullLogger<BlobDetectionOperator>.Instance);
        var blobOp = new Operator("BlobAnalysis", OperatorType.BlobAnalysis, 0, 0);
        blobOp.AddParameter(new Parameter(Guid.NewGuid(), "MinArea", "MinArea", string.Empty, "int", 20));
        blobOp.AddParameter(new Parameter(Guid.NewGuid(), "MaxArea", "MaxArea", string.Empty, "int", 100000));

        var blobStats = await MeasureAsync(
            async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = blobImage.AddRef() };
                var result = await blob.ExecuteAsync(blobOp, inputs);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                DisposeObjectGraph(result.OutputData);
            },
            warmupIterations,
            measuredIterations);

        AssertBudget("BlobAnalysis", BlobAnalysisBudgetMs, budgetScale, blobStats);

        // Caliper (<50ms @ 512x512).
        var caliper = new CaliperToolOperator(NullLogger<CaliperToolOperator>.Instance);
        var caliperOp = new Operator("Caliper", OperatorType.CaliperTool, 0, 0);
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "Direction", "Direction", string.Empty, "string", "Horizontal"));
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "Polarity", "Polarity", string.Empty, "string", "Both"));
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "EdgeThreshold", "EdgeThreshold", string.Empty, "double", 18.0));
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "ExpectedCount", "ExpectedCount", string.Empty, "int", 1));
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "MeasureMode", "MeasureMode", string.Empty, "string", "edge_pairs"));
        caliperOp.AddParameter(new Parameter(Guid.NewGuid(), "SubpixelAccuracy", "SubpixelAccuracy", string.Empty, "bool", false));

        var caliperStats = await MeasureAsync(
            async () =>
            {
                var inputs = new Dictionary<string, object> { ["Image"] = caliperImage.AddRef() };
                var result = await caliper.ExecuteAsync(caliperOp, inputs);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                DisposeObjectGraph(result.OutputData);
            },
            warmupIterations,
            measuredIterations);

        AssertBudget("CaliperTool", CaliperBudgetMs, budgetScale, caliperStats);

        // Undistort (<10ms @ 512x512).
        var undistort = new UndistortOperator(NullLogger<UndistortOperator>.Instance);
        var undistortOp = new Operator("Undistort", OperatorType.Undistort, 0, 0);

        var identityCalibration = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(width: 512, height: 512);

        var undistortStats = await MeasureAsync(
            async () =>
            {
                var inputs = new Dictionary<string, object>
                {
                    ["Image"] = undistortImage.AddRef(),
                    ["CalibrationData"] = identityCalibration
                };
                var result = await undistort.ExecuteAsync(undistortOp, inputs);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                DisposeObjectGraph(result.OutputData);
            },
            warmupIterations,
            measuredIterations);

        AssertBudget("Undistort", UndistortBudgetMs, budgetScale, undistortStats);
    }

    [Fact(Timeout = 240000)]
    public async Task W5_PerformanceBudget_CalibrationOperators_512_ShouldMeetBudgets()
    {
        var warmupIterations = GetEnvInt("CV_CAL_PERF_WARMUP_ITERS", 3, 0, 100);
        var measuredIterations = GetEnvInt("CV_CAL_PERF_MEASURE_ITERS", 15, 5, 300);
        var budgetScale = GetEnvDouble("CV_CAL_PERF_BUDGET_SCALE", 2.0, 0.5, 10.0);

        using var image = CreateUndistortBenchmarkImage(width: 512, height: 512);
        using var leftImage = CreateUndistortBenchmarkImage(width: 512, height: 512);
        using var rightImage = CreateUndistortBenchmarkImage(width: 512, height: 512);
        using var chessboard = CreateChessboardImage(boardWidth: 9, boardHeight: 6, squareSize: 40);

        var cameraBundle = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(width: 512, height: 512);
        var fisheyeBundle = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(fisheye: true, width: 512, height: 512);
        var scaleOffsetBundle = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        var tempCalibrationFile = Path.Combine(Path.GetTempPath(), $"calibration_bundle_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempCalibrationFile, scaleOffsetBundle);

        try
        {
            var (robotPosesSeed, boardPosesSeed) = CreateHandEyeSampleSet();
            var handEyeCalibrationData = await ResolveHandEyeCalibrationDataAsync(robotPosesSeed, boardPosesSeed);

            var cases = new List<(string Name, double BudgetMs, Func<Task> Action)>
            {
                ("CameraCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new CameraCalibrationOperator(NullLogger<CameraCalibrationOperator>.Instance);
                    var op = new Operator("CameraCalibration", OperatorType.CameraCalibration, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "PatternType", "PatternType", string.Empty, "string", "Chessboard"));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "BoardWidth", "BoardWidth", string.Empty, "int", 9));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "BoardHeight", "BoardHeight", string.Empty, "int", 6));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "SquareSize", "SquareSize", string.Empty, "double", 25.0));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "Mode", "Mode", string.Empty, "string", "SingleImage"));

                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = chessboard.AddRef() });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("Undistort", CalibrationConsumerBudgetMs, async () =>
                {
                    var executor = new UndistortOperator(NullLogger<UndistortOperator>.Instance);
                    var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["Image"] = image.AddRef(),
                        ["CalibrationData"] = cameraBundle
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("CoordinateTransform", CalibrationConsumerBudgetMs, async () =>
                {
                    var executor = new CoordinateTransformOperator(NullLogger<CoordinateTransformOperator>.Instance);
                    var op = new Operator("CoordinateTransform", OperatorType.CoordinateTransform, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "PixelX", "PixelX", string.Empty, "double", 128.0));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "PixelY", "PixelY", string.Empty, "double", 96.0));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationData", "CalibrationData", string.Empty, "string", scaleOffsetBundle));

                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["Image"] = image.AddRef(),
                        ["CalibrationData"] = scaleOffsetBundle
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("NPointCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new NPointCalibrationOperator(NullLogger<NPointCalibrationOperator>.Instance);
                    var op = new Operator("NPointCalibration", OperatorType.NPointCalibration, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationMode", "CalibrationMode", string.Empty, "string", "Affine"));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "PointPairs", "PointPairs", string.Empty, "string", CreateNPointPairsJson()));

                    var result = await executor.ExecuteAsync(op, null);
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("CalibrationLoader", CalibrationUtilityBudgetMs, async () =>
                {
                    var executor = new CalibrationLoaderOperator(NullLogger<CalibrationLoaderOperator>.Instance);
                    var op = new Operator("CalibrationLoader", OperatorType.CalibrationLoader, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "FilePath", "FilePath", string.Empty, "string", tempCalibrationFile));

                    var result = await executor.ExecuteAsync(op, null);
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("TranslationRotationCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new TranslationRotationCalibrationOperator(NullLogger<TranslationRotationCalibrationOperator>.Instance);
                    var op = new Operator("TranslationRotationCalibration", OperatorType.TranslationRotationCalibration, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "LeastSquares"));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationPoints", "CalibrationPoints", string.Empty, "string", CreateTranslationCalibrationPointsJson()));

                    var result = await executor.ExecuteAsync(op, null);
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("FisheyeCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new FisheyeCalibrationOperator(NullLogger<FisheyeCalibrationOperator>.Instance);
                    var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("FisheyeUndistort", CalibrationConsumerBudgetMs, async () =>
                {
                    var executor = new FisheyeUndistortOperator(NullLogger<FisheyeUndistortOperator>.Instance);
                    var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["Image"] = image.AddRef(),
                        ["CalibrationData"] = fisheyeBundle
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("HandEyeCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new HandEyeCalibrationOperator(NullLogger<HandEyeCalibrationOperator>.Instance);
                    var op = new Operator("HandEyeCalibration", OperatorType.HandEyeCalibration, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationType", "CalibrationType", string.Empty, "string", "eye_in_hand"));
                    op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "TSAI"));

                    var (robotPoses, boardPoses) = CreateHandEyeSampleSet();
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["RobotPoses"] = robotPoses,
                        ["CalibrationBoardPoses"] = boardPoses
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("HandEyeCalibrationValidator", CalibrationConsumerBudgetMs, async () =>
                {
                    var executor = new HandEyeCalibrationValidatorOperator(NullLogger<HandEyeCalibrationValidatorOperator>.Instance);
                    var op = new Operator("HandEyeCalibrationValidator", OperatorType.HandEyeCalibrationValidator, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationType", "CalibrationType", string.Empty, "string", "eye_in_hand"));

                    var (robotPoses, boardPoses) = CreateHandEyeSampleSet();
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["RobotPoses"] = robotPoses,
                        ["CalibrationBoardPoses"] = boardPoses,
                        ["CalibrationData"] = handEyeCalibrationData
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("StereoCalibration", CalibrationProducerBudgetMs, async () =>
                {
                    var executor = new StereoCalibrationOperator(NullLogger<StereoCalibrationOperator>.Instance);
                    var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["LeftImage"] = leftImage.AddRef(),
                        ["RightImage"] = rightImage.AddRef()
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                }),
                ("PixelToWorldTransform", CalibrationConsumerBudgetMs, async () =>
                {
                    var executor = new PixelToWorldTransformOperator(NullLogger<PixelToWorldTransformOperator>.Instance);
                    var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
                    op.AddParameter(new Parameter(Guid.NewGuid(), "TransformMode", "TransformMode", string.Empty, "string", "PixelToWorld"));

                    var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
                    {
                        ["Image"] = image.AddRef(),
                        ["CalibrationData"] = scaleOffsetBundle,
                        ["Points"] = new List<Position> { new(100, 100), new(200, 160) }
                    });
                    Assert.True(result.IsSuccess, result.ErrorMessage);
                    DisposeObjectGraph(result.OutputData);
                })
            };

            foreach (var item in cases)
            {
                var stats = await MeasureAsync(item.Action, warmupIterations, measuredIterations);
                AssertBudget(item.Name, item.BudgetMs, budgetScale, stats);
            }
        }
        finally
        {
            if (File.Exists(tempCalibrationFile))
            {
                File.Delete(tempCalibrationFile);
            }
        }
    }

    private static void AssertBudget(string name, double budgetMs, double scale, PerfStats stats)
    {
        var allowed = budgetMs * scale;
        Console.WriteLine(
            $"{name}: mean={stats.MeanMs:F2}ms p95={stats.P95Ms:F2}ms p99={stats.P99Ms:F2}ms (budget={budgetMs:F1}ms scale={scale:F2} allowed={allowed:F1}ms) runs={stats.Samples.Count}");

        Assert.True(
            stats.P95Ms <= allowed,
            $"{name} exceeded budget: p95={stats.P95Ms:F2}ms allowed={allowed:F2}ms (budget={budgetMs:F2}ms scale={scale:F2}).");
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

    private sealed record PerfStats(IReadOnlyList<double> Samples)
    {
        public double MeanMs => Samples.Count == 0 ? 0 : Samples.Average();
        public double P95Ms => Percentile(Samples, 0.95);
        public double P99Ms => Percentile(Samples, 0.99);
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
        index = Math.Clamp(index, 0, orderedValues.Count - 1);
        return orderedValues[index];
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

    private static ImageWrapper CreateStandardTemplate(int size)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(8, 10, size - 16, size - 20), Scalar.White, thickness: -1);
        Cv2.Circle(mat, new Point(size / 2, size / 2), size / 6, Scalar.Black, thickness: -1);
        Cv2.Line(mat, new Point(12, size - 14), new Point(size - 14, 12), Scalar.White, thickness: 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSceneByWarpingTemplate(Mat template, int width, int height, double x, double y)
    {
        using var transform = new Mat(2, 3, MatType.CV_64FC1);
        transform.Set(0, 0, 1.0); transform.Set(0, 1, 0.0); transform.Set(0, 2, x);
        transform.Set(1, 0, 0.0); transform.Set(1, 1, 1.0); transform.Set(1, 2, y);

        var scene = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.WarpAffine(template, scene, transform, new Size(width, height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return new ImageWrapper(scene);
    }

    private static ImageWrapper CreateBlobBenchmarkImage(int width, int height, int blobCount)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);

        var rnd = new Random(123);
        for (var i = 0; i < blobCount; i++)
        {
            var cx = rnd.Next(40, width - 40);
            var cy = rnd.Next(40, height - 40);
            var radius = rnd.Next(8, 18);
            Cv2.Circle(mat, new Point(cx, cy), radius, Scalar.White, thickness: -1);
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateCaliperBenchmarkImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        // High-contrast vertical bar to produce a single edge pair on a horizontal scan.
        Cv2.Rectangle(mat, new Rect(width / 2 - 20, height / 2 - 60, 40, 120), Scalar.White, thickness: -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateUndistortBenchmarkImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(width / 8, height / 8, width / 3, height / 4), new Scalar(255, 255, 255), thickness: -1);
        Cv2.Circle(mat, new Point(width / 2, height / 2), Math.Max(20, Math.Min(width, height) / 8), new Scalar(0, 0, 255), thickness: -1);
        Cv2.Line(mat, new Point(width / 4, height * 3 / 4), new Point(width * 3 / 4, height / 4), new Scalar(0, 255, 0), thickness: 3);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateChessboardImage(int boardWidth, int boardHeight, int squareSize)
    {
        var rows = boardHeight + 1;
        var cols = boardWidth + 1;
        var mat = new Mat(rows * squareSize, cols * squareSize, MatType.CV_8UC3, Scalar.White);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                if (((x + y) & 1) == 0)
                {
                    Cv2.Rectangle(
                        mat,
                        new Rect(x * squareSize, y * squareSize, squareSize, squareSize),
                        Scalar.Black,
                        -1);
                }
            }
        }

        Cv2.GaussianBlur(mat, mat, new Size(3, 3), 0);
        return new ImageWrapper(mat);
    }

    private static string CreateNPointPairsJson()
    {
        return "[" +
               "{\"ImageX\":0,\"ImageY\":0,\"WorldX\":0,\"WorldY\":0}," +
               "{\"ImageX\":10,\"ImageY\":0,\"WorldX\":20,\"WorldY\":0}," +
               "{\"ImageX\":0,\"ImageY\":10,\"WorldX\":0,\"WorldY\":20}," +
               "{\"ImageX\":20,\"ImageY\":15,\"WorldX\":40,\"WorldY\":30}" +
               "]";
    }

    private static string CreateTranslationCalibrationPointsJson()
    {
        return "[" +
               "{\"imageX\":0,\"imageY\":0,\"robotX\":10,\"robotY\":20,\"angle\":0}," +
               "{\"imageX\":10,\"imageY\":0,\"robotX\":20,\"robotY\":20,\"angle\":0}," +
               "{\"imageX\":0,\"imageY\":10,\"robotX\":10,\"robotY\":30,\"angle\":0}," +
               "{\"imageX\":20,\"imageY\":10,\"robotX\":30,\"robotY\":30,\"angle\":0}" +
               "]";
    }

    private static (List<Matrix4x4> RobotPoses, List<Matrix4x4> BoardPoses) CreateHandEyeSampleSet()
    {
        var cameraToTool = CreateTransform(new Vector3(0.030f, -0.015f, 0.080f), 5f, -8f, 12f);
        var targetToBase = CreateTransform(new Vector3(0.450f, 0.120f, 0.250f), 0f, 0f, 0f);
        Matrix4x4.Invert(cameraToTool, out var inverseCameraToTool);

        var robotPoses = new List<Matrix4x4>();
        var boardPoses = new List<Matrix4x4>();

        var samples = new[]
        {
            (new Vector3(0.10f, 0.02f, 0.35f), 0f, 5f, -8f),
            (new Vector3(0.12f, -0.04f, 0.32f), 6f, -4f, 15f),
            (new Vector3(0.08f, 0.06f, 0.38f), -10f, 7f, -12f),
            (new Vector3(0.15f, -0.01f, 0.40f), 8f, 9f, 4f),
            (new Vector3(0.18f, 0.03f, 0.34f), -6f, -7f, 18f)
        };

        foreach (var (translation, roll, pitch, yaw) in samples)
        {
            var baseToTool = CreateTransform(translation, roll, pitch, yaw);
            var targetToCamera = targetToBase * baseToTool * inverseCameraToTool;
            Matrix4x4.Invert(targetToCamera, out var cameraToTarget);

            robotPoses.Add(baseToTool);
            boardPoses.Add(cameraToTarget);
        }

        return (robotPoses, boardPoses);
    }

    private static Matrix4x4 CreateTransform(Vector3 translation, float rollDeg, float pitchDeg, float yawDeg)
    {
        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            DegreesToRadians(yawDeg),
            DegreesToRadians(pitchDeg),
            DegreesToRadians(rollDeg));
        rotation.M41 = translation.X;
        rotation.M42 = translation.Y;
        rotation.M43 = translation.Z;
        rotation.M44 = 1f;
        return rotation;
    }

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);

    private static async Task<string> ResolveHandEyeCalibrationDataAsync(
        List<Matrix4x4> robotPoses,
        List<Matrix4x4> boardPoses)
    {
        var executor = new HandEyeCalibrationOperator(NullLogger<HandEyeCalibrationOperator>.Instance);
        var op = new Operator("HandEyeCalibration", OperatorType.HandEyeCalibration, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationType", "CalibrationType", string.Empty, "string", "eye_in_hand"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "TSAI"));

        var result = await executor.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["RobotPoses"] = robotPoses,
            ["CalibrationBoardPoses"] = boardPoses
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return Assert.IsType<string>(result.OutputData!["CalibrationData"]);
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
}
