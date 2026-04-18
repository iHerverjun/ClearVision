using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
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
}
