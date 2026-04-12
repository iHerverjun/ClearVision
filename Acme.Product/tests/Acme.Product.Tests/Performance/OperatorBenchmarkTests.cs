using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Acme.Product.Tests.TestData;
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

    private static readonly OperatorType[] CalibrationBenchmarkTypes =
    [
        OperatorType.CameraCalibration,
        OperatorType.Undistort,
        OperatorType.CoordinateTransform,
        OperatorType.NPointCalibration,
        OperatorType.CalibrationLoader,
        OperatorType.TranslationRotationCalibration,
        OperatorType.FisheyeCalibration,
        OperatorType.FisheyeUndistort,
        OperatorType.HandEyeCalibration,
        OperatorType.HandEyeCalibrationValidator,
        OperatorType.StereoCalibration,
        OperatorType.PixelToWorldTransform
    ];

    private static readonly OperatorType[] MeasurementBenchmarkTypes =
    [
        OperatorType.Measurement,
        OperatorType.CircleMeasurement,
        OperatorType.LineMeasurement,
        OperatorType.ContourMeasurement,
        OperatorType.AngleMeasurement,
        OperatorType.GeometricTolerance,
        OperatorType.GeometricFitting,
        OperatorType.CaliperTool,
        OperatorType.WidthMeasurement,
        OperatorType.PointLineDistance,
        OperatorType.LineLineDistance,
        OperatorType.GapMeasurement,
        OperatorType.GeoMeasurement,
        OperatorType.SharpnessEvaluation,
        OperatorType.ColorMeasurement,
        OperatorType.HistogramAnalysis,
        OperatorType.PixelStatistics
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

    [Fact]
    public async Task Benchmark_CalibrationOperators_ShouldGenerateBaselineReport()
    {
        var entries = new List<BenchmarkEntry>();

        foreach (var type in CalibrationBenchmarkTypes)
        {
            var samples = await RunCalibrationBenchmarkAsync(type, width: 512, height: 512);
            if (samples.Count == 0)
            {
                _output.WriteLine($"[Skip] {type}: no successful sample.");
                continue;
            }

            var avg = samples.Average();
            var p95 = Percentile(samples, 0.95);
            var p99 = Percentile(samples, 0.99);
            var needsOptimization = avg > 100;

            var entry = new BenchmarkEntry(
                type,
                512,
                512,
                samples.Count,
                avg,
                p95,
                p99,
                needsOptimization);

            entries.Add(entry);
            _output.WriteLine(
                $"{type} @ 512x512: avg={avg:F2}ms, p95={p95:F2}ms, p99={p99:F2}ms, status={(needsOptimization ? "NeedOptimize" : "OK")}");
        }

        Assert.Equal(CalibrationBenchmarkTypes.Length, entries.Count);

        var reportPath = WriteCalibrationReport(entries);
        _output.WriteLine($"Calibration benchmark report written: {reportPath}");
    }

    [Fact]
    public async Task Benchmark_MeasurementOperators_ShouldGenerateBaselineReport()
    {
        var entries = new List<BenchmarkEntry>();

        foreach (var type in MeasurementBenchmarkTypes)
        {
            var samples = await RunMeasurementBenchmarkAsync(type, width: 512, height: 512);
            if (samples.Count == 0)
            {
                _output.WriteLine($"[Skip] {type}: no successful sample.");
                continue;
            }

            var avg = samples.Average();
            var p95 = Percentile(samples, 0.95);
            var p99 = Percentile(samples, 0.99);
            var needsOptimization = avg > 100;

            var entry = new BenchmarkEntry(
                type,
                512,
                512,
                samples.Count,
                avg,
                p95,
                p99,
                needsOptimization);

            entries.Add(entry);
            _output.WriteLine(
                $"{type} @ 512x512: avg={avg:F2}ms, p95={p95:F2}ms, p99={p99:F2}ms, status={(needsOptimization ? "NeedOptimize" : "OK")}");
        }

        Assert.Equal(MeasurementBenchmarkTypes.Length, entries.Count);

        var reportPath = WriteMeasurementReport(entries);
        _output.WriteLine($"Measurement benchmark report written: {reportPath}");
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

    private async Task<List<long>> RunCalibrationBenchmarkAsync(OperatorType type, int width, int height)
    {
        var iterations = 6;
        var samples = new List<long>(iterations);

        using var baseImage = CreateBenchmarkImage(width, height);
        using var leftImage = CreateBenchmarkImage(width, height);
        using var rightImage = CreateBenchmarkImage(width, height);
        using var chessboard = CreateChessboardImage(boardWidth: 9, boardHeight: 6, squareSize: 40);

        var cameraBundle = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(width: width, height: height);
        var fisheyeBundle = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(fisheye: true, width: width, height: height);
        var scaleOffsetBundle = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        var calibrationFile = Path.Combine(Path.GetTempPath(), $"benchmark_calibration_{Guid.NewGuid():N}.json");
        File.WriteAllText(calibrationFile, scaleOffsetBundle);

        try
        {
            var (robotPoses, boardPoses) = CreateHandEyeSampleSet();
            var handEyeCalibrationData = await ResolveHandEyeCalibrationDataAsync(robotPoses, boardPoses);

            _ = await ExecuteCalibrationSampleAsync(
                type,
                baseImage,
                leftImage,
                rightImage,
                chessboard,
                cameraBundle,
                fisheyeBundle,
                scaleOffsetBundle,
                calibrationFile,
                robotPoses,
                boardPoses,
                handEyeCalibrationData);

            for (var i = 0; i < iterations; i++)
            {
                var elapsed = await ExecuteCalibrationSampleAsync(
                    type,
                    baseImage,
                    leftImage,
                    rightImage,
                    chessboard,
                    cameraBundle,
                    fisheyeBundle,
                    scaleOffsetBundle,
                    calibrationFile,
                    robotPoses,
                    boardPoses,
                    handEyeCalibrationData);

                if (elapsed >= 0)
                {
                    samples.Add(elapsed);
                }
            }

            return samples;
        }
        finally
        {
            if (File.Exists(calibrationFile))
            {
                File.Delete(calibrationFile);
            }
        }
    }

    private async Task<List<long>> RunMeasurementBenchmarkAsync(OperatorType type, int width, int height)
    {
        var iterations = 6;
        var samples = new List<long>(iterations);

        using var measurementImage = CreateMeasurementBenchmarkImage(width, height);
        using var circleImage = CreateCircleBenchmarkImage(width, height);
        using var lineImage = CreateLineBenchmarkImage(width, height);
        using var caliperImage = CreateCaliperBenchmarkImage(width, height);
        using var gapImage = CreateGapBenchmarkImage(width, height);
        using var widthImage = CreateWidthBenchmarkImage(width, height);

        _ = await ExecuteMeasurementSampleAsync(type, measurementImage, circleImage, lineImage, caliperImage, gapImage, widthImage);
        for (var i = 0; i < iterations; i++)
        {
            var elapsed = await ExecuteMeasurementSampleAsync(type, measurementImage, circleImage, lineImage, caliperImage, gapImage, widthImage);
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

    private async Task<long> ExecuteCalibrationSampleAsync(
        OperatorType type,
        Mat baseImage,
        Mat leftImage,
        Mat rightImage,
        Mat chessboard,
        string cameraBundle,
        string fisheyeBundle,
        string scaleOffsetBundle,
        string calibrationFile,
        List<Matrix4x4> robotPoses,
        List<Matrix4x4> boardPoses,
        string handEyeCalibrationData)
    {
        IOperatorExecutor executor;
        var op = new Operator($"{type}_Benchmark", type, 0, 0);
        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        switch (type)
        {
            case OperatorType.CameraCalibration:
                executor = new CameraCalibrationOperator(NullLogger<CameraCalibrationOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "PatternType", "PatternType", string.Empty, "string", "Chessboard"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "BoardWidth", "BoardWidth", string.Empty, "int", 9));
                op.AddParameter(new Parameter(Guid.NewGuid(), "BoardHeight", "BoardHeight", string.Empty, "int", 6));
                op.AddParameter(new Parameter(Guid.NewGuid(), "SquareSize", "SquareSize", string.Empty, "double", 25.0));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Mode", "Mode", string.Empty, "string", "SingleImage"));
                inputs["Image"] = new ImageWrapper(chessboard.Clone());
                break;
            case OperatorType.Undistort:
                executor = new UndistortOperator(NullLogger<UndistortOperator>.Instance);
                inputs["Image"] = new ImageWrapper(baseImage.Clone());
                inputs["CalibrationData"] = cameraBundle;
                break;
            case OperatorType.CoordinateTransform:
                executor = new CoordinateTransformOperator(NullLogger<CoordinateTransformOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "PixelX", "PixelX", string.Empty, "double", 128.0));
                op.AddParameter(new Parameter(Guid.NewGuid(), "PixelY", "PixelY", string.Empty, "double", 96.0));
                op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationData", "CalibrationData", string.Empty, "string", scaleOffsetBundle));
                inputs["Image"] = new ImageWrapper(baseImage.Clone());
                inputs["CalibrationData"] = scaleOffsetBundle;
                break;
            case OperatorType.NPointCalibration:
                executor = new NPointCalibrationOperator(NullLogger<NPointCalibrationOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationMode", "CalibrationMode", string.Empty, "string", "Affine"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "PointPairs", "PointPairs", string.Empty, "string", CreateNPointPairsJson()));
                break;
            case OperatorType.CalibrationLoader:
                executor = new CalibrationLoaderOperator(NullLogger<CalibrationLoaderOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "FilePath", "FilePath", string.Empty, "string", calibrationFile));
                break;
            case OperatorType.TranslationRotationCalibration:
                executor = new TranslationRotationCalibrationOperator(NullLogger<TranslationRotationCalibrationOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "LeastSquares"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationPoints", "CalibrationPoints", string.Empty, "string", CreateTranslationCalibrationPointsJson()));
                break;
            case OperatorType.FisheyeCalibration:
                executor = new FisheyeCalibrationOperator(NullLogger<FisheyeCalibrationOperator>.Instance);
                inputs["Image"] = new ImageWrapper(baseImage.Clone());
                break;
            case OperatorType.FisheyeUndistort:
                executor = new FisheyeUndistortOperator(NullLogger<FisheyeUndistortOperator>.Instance);
                inputs["Image"] = new ImageWrapper(baseImage.Clone());
                inputs["CalibrationData"] = fisheyeBundle;
                break;
            case OperatorType.HandEyeCalibration:
                executor = new HandEyeCalibrationOperator(NullLogger<HandEyeCalibrationOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationType", "CalibrationType", string.Empty, "string", "eye_in_hand"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "TSAI"));
                inputs["RobotPoses"] = robotPoses;
                inputs["CalibrationBoardPoses"] = boardPoses;
                break;
            case OperatorType.HandEyeCalibrationValidator:
                executor = new HandEyeCalibrationValidatorOperator(NullLogger<HandEyeCalibrationValidatorOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationType", "CalibrationType", string.Empty, "string", "eye_in_hand"));
                inputs["RobotPoses"] = robotPoses;
                inputs["CalibrationBoardPoses"] = boardPoses;
                inputs["CalibrationData"] = handEyeCalibrationData;
                break;
            case OperatorType.StereoCalibration:
                executor = new StereoCalibrationOperator(NullLogger<StereoCalibrationOperator>.Instance);
                inputs["LeftImage"] = new ImageWrapper(leftImage.Clone());
                inputs["RightImage"] = new ImageWrapper(rightImage.Clone());
                break;
            case OperatorType.PixelToWorldTransform:
                executor = new PixelToWorldTransformOperator(NullLogger<PixelToWorldTransformOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "TransformMode", "TransformMode", string.Empty, "string", "PixelToWorld"));
                inputs["Image"] = new ImageWrapper(baseImage.Clone());
                inputs["CalibrationData"] = scaleOffsetBundle;
                inputs["Points"] = new List<Position> { new(100, 100), new(220, 180) };
                break;
            default:
                _output.WriteLine($"[Skip sample] {type}: no calibration benchmark setup.");
                return -1;
        }

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

    private async Task<long> ExecuteMeasurementSampleAsync(
        OperatorType type,
        Mat measurementImage,
        Mat circleImage,
        Mat lineImage,
        Mat caliperImage,
        Mat gapImage,
        Mat widthImage)
    {
        IOperatorExecutor executor;
        var op = new Operator($"{type}_Benchmark", type, 0, 0);
        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        switch (type)
        {
            case OperatorType.Measurement:
                executor = new MeasureDistanceOperator(NullLogger<MeasureDistanceOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "MeasureType", "MeasureType", string.Empty, "string", "PointToPoint"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "X1", "X1", string.Empty, "int", 80));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Y1", "Y1", string.Empty, "int", 110));
                op.AddParameter(new Parameter(Guid.NewGuid(), "X2", "X2", string.Empty, "int", 420));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Y2", "Y2", string.Empty, "int", 360));
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            case OperatorType.CircleMeasurement:
                executor = new CircleMeasurementOperator(NullLogger<CircleMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "HoughCircle"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "MinRadius", "MinRadius", string.Empty, "int", 30));
                op.AddParameter(new Parameter(Guid.NewGuid(), "MaxRadius", "MaxRadius", string.Empty, "int", 140));
                inputs["Image"] = new ImageWrapper(circleImage.Clone());
                break;
            case OperatorType.LineMeasurement:
                executor = new LineMeasurementOperator(NullLogger<LineMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "Method", "Method", string.Empty, "string", "FitLine"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "MinLength", "MinLength", string.Empty, "double", 80.0));
                inputs["Image"] = new ImageWrapper(lineImage.Clone());
                break;
            case OperatorType.ContourMeasurement:
                executor = new ContourMeasurementOperator(NullLogger<ContourMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "Threshold", "Threshold", string.Empty, "double", 100.0));
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            case OperatorType.AngleMeasurement:
                executor = new AngleMeasurementOperator(NullLogger<AngleMeasurementOperator>.Instance);
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            case OperatorType.GeometricTolerance:
                executor = new GeometricToleranceOperator(NullLogger<GeometricToleranceOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "ToleranceType", "ToleranceType", string.Empty, "string", "Parallelism"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "ZoneSize", "ZoneSize", string.Empty, "double", 2.0));
                inputs["FeaturePrimary"] = new LineData(60, 120, 460, 120);
                inputs["DatumA"] = new LineData(60, 220, 460, 220);
                break;
            case OperatorType.GeometricFitting:
                executor = new GeometricFittingOperator(NullLogger<GeometricFittingOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "FitType", "FitType", string.Empty, "string", "Circle"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Threshold", "Threshold", string.Empty, "double", 100.0));
                inputs["Image"] = new ImageWrapper(circleImage.Clone());
                break;
            case OperatorType.CaliperTool:
                executor = new CaliperToolOperator(NullLogger<CaliperToolOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "MeasureMode", "MeasureMode", string.Empty, "string", "edge_pairs"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "ExpectedCount", "ExpectedCount", string.Empty, "int", 1));
                inputs["Image"] = new ImageWrapper(caliperImage.Clone());
                break;
            case OperatorType.WidthMeasurement:
                executor = new WidthMeasurementOperator(NullLogger<WidthMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "MeasureMode", "MeasureMode", string.Empty, "string", "ManualLines"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "SampleCount", "SampleCount", string.Empty, "int", 20));
                op.AddParameter(new Parameter(Guid.NewGuid(), "MultiScanCount", "MultiScanCount", string.Empty, "int", 24));
                inputs["Image"] = new ImageWrapper(widthImage.Clone());
                inputs["Line1"] = new LineData(180, 120, 180, 390);
                inputs["Line2"] = new LineData(300, 120, 300, 390);
                break;
            case OperatorType.PointLineDistance:
                executor = new PointLineDistanceOperator(NullLogger<PointLineDistanceOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceModel", "DistanceModel", string.Empty, "string", "Segment"));
                inputs["Point"] = new Position(240, 210);
                inputs["Line"] = new LineData(60, 200, 460, 200);
                break;
            case OperatorType.LineLineDistance:
                executor = new LineLineDistanceOperator(NullLogger<LineLineDistanceOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceModel", "DistanceModel", string.Empty, "string", "Segment"));
                inputs["Line1"] = new LineData(20, 20, 420, 20);
                inputs["Line2"] = new LineData(20, 160, 420, 160);
                break;
            case OperatorType.GapMeasurement:
                executor = new GapMeasurementOperator(NullLogger<GapMeasurementOperator>.Instance);
                inputs["Image"] = new ImageWrapper(gapImage.Clone());
                break;
            case OperatorType.GeoMeasurement:
                executor = new GeoMeasurementOperator(NullLogger<GeoMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "Element1Type", "Element1Type", string.Empty, "string", "Line"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "Element2Type", "Element2Type", string.Empty, "string", "Line"));
                op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceModel", "DistanceModel", string.Empty, "string", "Segment"));
                inputs["Element1"] = new LineData(60, 200, 460, 200);
                inputs["Element2"] = new LineData(260, 40, 260, 460);
                break;
            case OperatorType.SharpnessEvaluation:
                executor = new SharpnessEvaluationOperator(NullLogger<SharpnessEvaluationOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "ThresholdMode", "ThresholdMode", string.Empty, "string", "PerMethodDefault"));
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            case OperatorType.ColorMeasurement:
                executor = new ColorMeasurementOperator(NullLogger<ColorMeasurementOperator>.Instance);
                op.AddParameter(new Parameter(Guid.NewGuid(), "MeasurementMode", "MeasurementMode", string.Empty, "string", "LabDeltaE"));
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                inputs["ReferenceColor"] = new Dictionary<string, object> { ["L"] = 0.0, ["A"] = 0.0, ["B"] = 0.0 };
                break;
            case OperatorType.HistogramAnalysis:
                executor = new HistogramAnalysisOperator(NullLogger<HistogramAnalysisOperator>.Instance);
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            case OperatorType.PixelStatistics:
                executor = new PixelStatisticsOperator(NullLogger<PixelStatisticsOperator>.Instance);
                inputs["Image"] = new ImageWrapper(measurementImage.Clone());
                break;
            default:
                _output.WriteLine($"[Skip sample] {type}: no measurement benchmark setup.");
                return -1;
        }

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

    private static Mat CreateMeasurementBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(image, new Rect(50, 50, width / 3, height / 4), Scalar.White, -1);
        Cv2.Rectangle(image, new Rect(width / 2, height / 2, width / 3 - 20, height / 3 - 20), new Scalar(180, 180, 180), -1);
        Cv2.Circle(image, new CvPoint(width / 2, height / 3), 70, new Scalar(255, 255, 255), 2);
        Cv2.Line(image, new CvPoint(30, height - 40), new CvPoint(width - 30, 40), new Scalar(0, 255, 0), 3);
        return image;
    }

    private static Mat CreateCircleBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Circle(image, new CvPoint(width / 2, height / 2), 90, Scalar.White, 3);
        return image;
    }

    private static Mat CreateLineBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Line(image, new CvPoint(40, 80), new CvPoint(width - 40, 100), Scalar.White, 2);
        Cv2.Line(image, new CvPoint(70, height / 2), new CvPoint(width - 70, height / 2), Scalar.White, 3);
        return image;
    }

    private static Mat CreateCaliperBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(image, new Rect(width / 2 - 60, height / 4, 120, height / 2), Scalar.White, -1);
        return image;
    }

    private static Mat CreateGapBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        foreach (var x in new[] { 80, 160, 240, 320, 400 })
        {
            Cv2.Line(image, new CvPoint(x, 30), new CvPoint(x, height - 30), Scalar.White, 2);
        }

        return image;
    }

    private static Mat CreateWidthBenchmarkImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(image, new Rect(190, 110, 100, 290), Scalar.White, -1);
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

    private static string WriteCalibrationReport(IReadOnlyList<BenchmarkEntry> entries)
    {
        var repoRoot = ResolveAcmeProductRoot();
        var reportDirectory = Path.Combine(repoRoot, "test_results");

        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, "calibration_operator_benchmark_report.md");
        var builder = new StringBuilder();

        builder.AppendLine("# Calibration Operator Benchmark Report");
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

    private static string WriteMeasurementReport(IReadOnlyList<BenchmarkEntry> entries)
    {
        var repoRoot = ResolveAcmeProductRoot();
        var reportDirectory = Path.Combine(repoRoot, "test_results");

        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, "measurement_operator_benchmark_report.md");
        var builder = new StringBuilder();

        builder.AppendLine("# Measurement Operator Benchmark Report");
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

    private static Mat CreateChessboardImage(int boardWidth, int boardHeight, int squareSize)
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
                    Cv2.Rectangle(mat, new Rect(x * squareSize, y * squareSize, squareSize, squareSize), Scalar.Black, -1);
                }
            }
        }

        Cv2.GaussianBlur(mat, mat, new Size(3, 3), 0);
        return mat;
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

        if (!result.IsSuccess || result.OutputData == null)
        {
            throw new InvalidOperationException($"Failed to resolve hand-eye matrix for benchmark: {result.ErrorMessage}");
        }

        if (result.OutputData["CalibrationData"] is not string calibrationData)
        {
            throw new InvalidOperationException("Failed to resolve hand-eye calibration data for benchmark: output calibration data is missing.");
        }

        return calibrationData;
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
