using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.ImageProcessing;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.Product.Tests.Integration;

public sealed class MeasurementStabilityIntegrationTests
{
    [Fact]
    public async Task WidthMeasurement_Repeat100_ShouldRemainStable()
    {
        var exec = new WidthMeasurementOperator(NullLogger<WidthMeasurementOperator>.Instance);
        var op = new Operator("width", OperatorType.WidthMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureMode", "ManualLines", "string"));
        op.AddParameter(TestHelpers.CreateParameter("SampleCount", 20, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MultiScanCount", 24, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MinValidSamples", 8, "int"));

        using var image = CreateWidthImage();
        var widths = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = image.AddRef(),
                ["Line1"] = new LineData(48, 25, 48, 135),
                ["Line2"] = new LineData(102, 25, 102, 135)
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            widths.Add(Convert.ToDouble(result.OutputData!["Width"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(widths).Should().BeLessThan(0.2);
    }

    [Fact]
    public async Task ColorMeasurement_HsvStats_Repeat100_ShouldRemainStable()
    {
        var exec = new ColorMeasurementOperator(NullLogger<ColorMeasurementOperator>.Instance);
        var op = new Operator("color", OperatorType.ColorMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasurementMode", "HsvStats", "string"));

        using var image = CreateHueWrapImage();
        var hues = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            hues.Add(Convert.ToDouble(result.OutputData!["HueMean"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(hues).Should().BeLessThan(1e-6);
    }

    [Fact]
    public async Task CaliperTool_Subpixel_Repeat100_ShouldRemainStable()
    {
        var exec = new CaliperToolOperator(NullLogger<CaliperToolOperator>.Instance);
        var op = new Operator("caliper", OperatorType.CaliperTool, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Direction", "Horizontal", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Polarity", "Both", "string"));
        op.AddParameter(TestHelpers.CreateParameter("PairDirection", "positive_to_negative", "string"));
        op.AddParameter(TestHelpers.CreateParameter("EdgeThreshold", 6.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectedCount", 1, "int"));
        op.AddParameter(TestHelpers.CreateParameter("SubpixelAccuracy", true, "bool"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledVerticalStripeImage(240, 120, 90.25, 130.75, 12, 108);
        var widths = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = image.AddRef(),
                ["SearchRegion"] = new Rect(60, 8, 120, 104)
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            widths.Add(Convert.ToDouble(result.OutputData!["Width"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(widths).Should().BeLessThan(0.02);
    }

    [Fact]
    public async Task GapMeasurement_Repeat100_ShouldRemainStable()
    {
        var exec = new GapMeasurementOperator(NullLogger<GapMeasurementOperator>.Instance);
        var op = new Operator("gap", OperatorType.GapMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Direction", "Horizontal", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectedCount", 2, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RobustMode", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("MultiScanCount", 16, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateStripesImage(
            width: 140,
            height: 80,
            stripes: new[]
            {
                (20.0, 30.0, 10.0, 70.0),
                (48.5, 63.5, 10.0, 70.0),
                (82.0, 92.0, 10.0, 70.0)
            });

        var means = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            means.Add(Convert.ToDouble(result.OutputData!["MeanGap"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(means).Should().BeLessThan(0.02);
    }

    [Fact]
    public async Task LineMeasurement_Repeat50_ShouldRemainStable()
    {
        var exec = new LineMeasurementOperator(NullLogger<LineMeasurementOperator>.Instance);
        var op = new Operator("line", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "FitLine", "string"));
        op.AddParameter(TestHelpers.CreateParameter("MinLength", 160.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 70, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateLineImage(
            width: 240,
            height: 220,
            start: new Point2d(24.5, 166.25),
            end: new Point2d(216.5, 58.25),
            thicknessPx: 6.0);

        var angles = new List<double>(50);
        for (var i = 0; i < 50; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            angles.Add(Convert.ToDouble(result.OutputData!["Angle"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(angles).Should().BeLessThan(0.01);
    }

    [Fact]
    public async Task PixelToWorld_PlanarRepeat100_ShouldRemainStable()
    {
        var exec = new PixelToWorldTransformOperator(NullLogger<PixelToWorldTransformOperator>.Instance);
        var op = new Operator("p2w", OperatorType.PixelToWorldTransform, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("TransformMode", "PixelToWorld", "string"));

        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var points = new List<Position> { new(160, 120) };
        var worlds = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = image.AddRef(),
                ["CalibrationData"] = Acme.Product.Tests.TestData.CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson(),
                ["Points"] = points
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            var world = ((List<Point3d>)result.OutputData!["TransformedPoints"]).Single();
            worlds.Add(world.X);
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(worlds).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task MeasureDistance_SubpixelRepeat100_ShouldRemainStable()
    {
        var exec = new MeasureDistanceOperator(NullLogger<MeasureDistanceOperator>.Instance);
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "PointToPoint", "string"));

        var pointA = new Position(10.25, 20.50);
        var pointB = new Position(42.75, 63.125);
        var distances = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["PointA"] = pointA,
                ["PointB"] = pointB
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            distances.Add(Convert.ToDouble(result.OutputData!["Distance"]));
        }

        ComputeStdDev(distances).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task ContourMeasurement_Repeat100_ShouldRemainStable()
    {
        var exec = new ContourMeasurementOperator(NullLogger<ContourMeasurementOperator>.Instance);
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 96.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 2000, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledRotatedRectangleImage(
            width: 320,
            height: 240,
            center: new Point2d(160.0, 120.0),
            size: new Size2d(88.0, 46.0),
            angleDeg: 27.0,
            supersample: 16);

        var areas = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            areas.Add(Convert.ToDouble(result.OutputData!["Area"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(areas).Should().BeLessThan(0.05);
    }

    [Fact]
    public async Task AngleMeasurement_Repeat100_ShouldRemainStable()
    {
        var exec = new AngleMeasurementOperator(NullLogger<AngleMeasurementOperator>.Instance);
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Unit", "Degree", "string"));

        using var image = TestHelpers.CreateTestImage(width: 160, height: 120);
        var angles = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = image.AddRef(),
                ["Line1"] = new LineData(20.25f, 50.0f, 80.25f, 50.0f),
                ["Line2"] = new LineData(50.5f, 50.0f, 80.5f, 80.0f)
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            angles.Add(Convert.ToDouble(result.OutputData!["Angle"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(angles).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task CircleMeasurement_Repeat100_ShouldRemainStable()
    {
        var exec = new CircleMeasurementOperator(NullLogger<CircleMeasurementOperator>.Instance);
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "FitEllipse", "enum"));
        op.AddParameter(TestHelpers.CreateParameter("MinRadius", 60, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MaxRadius", 84, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledCircleImage(
            width: 420,
            height: 320,
            center: new Point2d(210.0, 154.0),
            radius: 72.0,
            supersample: 16);

        var radii = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = image.AddRef() });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            radii.Add(Convert.ToDouble(result.OutputData!["Radius"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(radii).Should().BeLessThan(0.05);
    }

    [Fact]
    public async Task GeometricTolerance_PositionRepeat100_ShouldRemainStable()
    {
        var exec = new GeometricToleranceOperator(NullLogger<GeometricToleranceOperator>.Instance);
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 0.02, "double"));
        op.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "CircularZone", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var deviations = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["FeaturePrimary"] = new Dictionary<string, object> { ["X"] = 10.003, ["Y"] = 5.004, ["UncertaintyPx"] = 0.05 },
                ["DatumA"] = new Dictionary<string, object> { ["StartX"] = 0.0, ["StartY"] = 0.0, ["EndX"] = 20.0, ["EndY"] = 0.0, ["UncertaintyPx"] = 0.05 },
                ["DatumB"] = new Dictionary<string, object> { ["StartX"] = 0.0, ["StartY"] = 0.0, ["EndX"] = 0.0, ["EndY"] = 20.0, ["UncertaintyPx"] = 0.05 }
            });

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            deviations.Add(Convert.ToDouble(result.OutputData!["ZoneDeviation"]));
        }

        ComputeStdDev(deviations).Should().BeLessThan(1e-12);
    }

    [Fact]
    public async Task GeometricFitting_CircleRepeat50_ShouldRemainStable()
    {
        var exec = new GeometricFittingOperator(NullLogger<GeometricFittingOperator>.Instance);
        var op = new Operator("geofit", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Circle", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 96.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 2000, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledCircleImage(320, 240, new Point2d(120.4, 99.6), 52.3, supersample: 16);
        var radii = new List<double>(50);
        for (var i = 0; i < 50; i++)
        {
            var result = await exec.ExecuteAsync(op, TestHelpers.CreateImageInputs(image.AddRef()));
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            var geometry = ((Dictionary<string, object>)result.OutputData!["FitResult"])["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;
            radii.Add(Convert.ToDouble(geometry["Radius"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(radii).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task SharpnessEvaluation_LaplacianRepeat100_ShouldRemainStable()
    {
        var exec = new SharpnessEvaluationOperator(NullLogger<SharpnessEvaluationOperator>.Instance);
        var op = new Operator("sharp", OperatorType.SharpnessEvaluation, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "Laplacian", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ThresholdMode", "Manual", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 100.0, "double"));

        using var image = CreateSharpnessPatternImage();
        var scores = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, TestHelpers.CreateImageInputs(image.AddRef()));
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            scores.Add(Convert.ToDouble(result.OutputData!["Score"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(scores).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task HistogramAnalysis_Repeat100_ShouldRemainStable()
    {
        var exec = new HistogramAnalysisOperator(NullLogger<HistogramAnalysisOperator>.Instance);
        var op = new Operator("hist", OperatorType.HistogramAnalysis, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Channel", "Gray", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BinCount", 256, "int"));

        using var image = CreateDiscreteDistributionImage();
        var means = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, TestHelpers.CreateImageInputs(image.AddRef()));
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            means.Add(Convert.ToDouble(result.OutputData!["Mean"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(means).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task PixelStatistics_Repeat100_ShouldRemainStable()
    {
        var exec = new PixelStatisticsOperator(NullLogger<PixelStatisticsOperator>.Instance);
        var op = new Operator("pixelstats", OperatorType.PixelStatistics, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Channel", "Gray", "string"));

        using var image = CreateConstantGrayImage(80, 60, 137);
        var means = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var result = await exec.ExecuteAsync(op, TestHelpers.CreateImageInputs(image.AddRef()));
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            means.Add(Convert.ToDouble(result.OutputData!["Mean"]));
        }

        ComputeStdDev(means).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task ColorMeasurement_LabDeltaERepeat100_ShouldRemainStable()
    {
        var exec = new ColorMeasurementOperator(NullLogger<ColorMeasurementOperator>.Instance);
        var referenceLab = CieLabConverter.BgrToLab(20, 40, 180);
        var op = new Operator("colorlab", OperatorType.ColorMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasurementMode", "LabDeltaE", "string"));
        op.AddParameter(TestHelpers.CreateParameter("DeltaEMethod", "CIEDE2000", "string"));

        using var image = new ImageWrapper(new Mat(60, 60, MatType.CV_8UC3, new Scalar(30, 30, 200)));
        var deltaEs = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var inputs = TestHelpers.CreateImageInputs(image.AddRef());
            inputs["ReferenceColor"] = new Dictionary<string, object>
            {
                ["L"] = referenceLab.L,
                ["A"] = referenceLab.A,
                ["B"] = referenceLab.B
            };

            var result = await exec.ExecuteAsync(op, inputs);
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            deltaEs.Add(Convert.ToDouble(result.OutputData!["DeltaE"]));
            (result.OutputData["Image"] as ImageWrapper)?.Dispose();
        }

        ComputeStdDev(deltaEs).Should().BeLessThan(1e-9);
    }

    private static double ComputeStdDev(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1);
        return Math.Sqrt(Math.Max(variance, 0.0));
    }

    private static ImageWrapper CreateWidthImage()
    {
        var mat = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 20, 50, 130), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateHueWrapImage()
    {
        var mat = new Mat(60, 60, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                mat.Set(y, x, x < 30 ? new Vec3b(0, 0, 255) : new Vec3b(10, 10, 255));
            }
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSharpnessPatternImage()
    {
        var mat = new Mat(200, 200, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(10, 10), new Point(190, 10), Scalar.White, 2);
        Cv2.Line(mat, new Point(10, 30), new Point(190, 180), Scalar.White, 2);
        Cv2.Rectangle(mat, new Rect(60, 60, 80, 80), Scalar.White, 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateDiscreteDistributionImage()
    {
        var values = Enumerable.Repeat((byte)10, 20)
            .Concat(Enumerable.Repeat((byte)60, 29))
            .Concat(Enumerable.Repeat((byte)140, 26))
            .Concat(Enumerable.Repeat((byte)220, 25))
            .ToArray();

        var mat = new Mat(10, 10, MatType.CV_8UC3);
        for (var index = 0; index < values.Length; index++)
        {
            var y = index / 10;
            var x = index % 10;
            var value = values[index];
            mat.Set(y, x, new Vec3b(value, value, value));
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateConstantGrayImage(int width, int height, byte value)
    {
        return new ImageWrapper(new Mat(height, width, MatType.CV_8UC1, new Scalar(value)));
    }
}
