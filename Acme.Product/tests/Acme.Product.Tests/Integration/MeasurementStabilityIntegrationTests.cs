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
