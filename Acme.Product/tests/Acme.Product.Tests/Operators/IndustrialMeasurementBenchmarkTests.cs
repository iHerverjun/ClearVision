using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class IndustrialMeasurementBenchmarkTests
{
    private readonly CircleMeasurementOperator _circleOperator;
    private readonly LineMeasurementOperator _lineOperator;

    public IndustrialMeasurementBenchmarkTests()
    {
        _circleOperator = new CircleMeasurementOperator(Substitute.For<ILogger<CircleMeasurementOperator>>());
        _lineOperator = new LineMeasurementOperator(Substitute.For<ILogger<LineMeasurementOperator>>());
    }

    [Fact]
    public async Task CircleMeasurement_RealIndustrialSample_ShouldStayWithinBaseline()
    {
        var baseline = LoadBaseline().CircleBaseline;
        using var sample = LoadSampleCrop(baseline.Crop);
        var op = new Operator("circle-industrial", OperatorType.CircleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", baseline.Method, "string"));
        op.AddParameter(TestHelpers.CreateParameter("MinRadius", baseline.MinRadius, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MaxRadius", baseline.MaxRadius, "int"));

        var result = await _circleOperator.ExecuteAsync(op, TestHelpers.CreateImageInputs(new ImageWrapper(sample.Clone())));
        result.IsSuccess.Should().BeTrue(result.ErrorMessage);

        var center = result.OutputData!["Center"].Should().BeOfType<Position>().Subject;
        var radius = Convert.ToDouble(result.OutputData["Radius"]);
        center.X.Should().BeApproximately(baseline.ExpectedCenterX, baseline.CenterTolerance);
        center.Y.Should().BeApproximately(baseline.ExpectedCenterY, baseline.CenterTolerance);
        radius.Should().BeApproximately(baseline.ExpectedRadius, baseline.RadiusTolerance);
    }

    [Fact]
    public async Task LineMeasurement_RealIndustrialSample_ShouldStayWithinBaseline()
    {
        var baseline = LoadBaseline().LineBaseline;
        using var sample = LoadSampleCrop(baseline.Crop);
        var op = new Operator("line-industrial", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", baseline.Method, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", baseline.Threshold, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MinLength", baseline.MinLength, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MaxGap", baseline.MaxGap, "double"));

        var result = await _lineOperator.ExecuteAsync(op, TestHelpers.CreateImageInputs(new ImageWrapper(sample.Clone())));
        result.IsSuccess.Should().BeTrue(result.ErrorMessage);

        var angle = Convert.ToDouble(result.OutputData!["Angle"]);
        var length = Convert.ToDouble(result.OutputData["Length"]);
        var residualMean = Convert.ToDouble(result.OutputData["ResidualMean"]);

        angle.Should().BeApproximately(baseline.ExpectedAngle, baseline.AngleTolerance);
        length.Should().BeInRange(baseline.ExpectedMinLength, baseline.ExpectedMaxLength);
        residualMean.Should().BeLessThan(baseline.MaxResidualMean);
    }

    private static IndustrialMeasurementBaseline LoadBaseline()
    {
        var repoRoot = FindRepoRoot();
        var baselinePath = Path.Combine(repoRoot, "Acme.Product", "tests", "TestData", "industrial_measurement_benchmark.json");
        var json = File.ReadAllText(baselinePath);
        return JsonSerializer.Deserialize<IndustrialMeasurementBaseline>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static Mat LoadSampleCrop(BenchmarkCrop crop)
    {
        var repoRoot = FindRepoRoot();
        var baseline = LoadBaseline();
        var samplePath = Path.Combine(repoRoot, baseline.SamplePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(samplePath).Should().BeTrue($"industrial benchmark sample should exist at {samplePath}");
        using var encoded = Cv2.ImDecode(File.ReadAllBytes(samplePath), ImreadModes.Color);
        encoded.Empty().Should().BeFalse($"industrial benchmark sample should decode successfully from {samplePath}");
        var roi = new Rect(crop.X, crop.Y, crop.Width, crop.Height);
        return new Mat(encoded, roi).Clone();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Acme.Product")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    private sealed class IndustrialMeasurementBaseline
    {
        public string SamplePath { get; set; } = string.Empty;
        public CircleBaseline CircleBaseline { get; set; } = new();
        public LineBaseline LineBaseline { get; set; } = new();
    }

    private sealed class CircleBaseline
    {
        public BenchmarkCrop Crop { get; set; } = new();
        public string Method { get; set; } = "HoughCircle";
        public int MinRadius { get; set; }
        public int MaxRadius { get; set; }
        public double ExpectedCenterX { get; set; }
        public double ExpectedCenterY { get; set; }
        public double ExpectedRadius { get; set; }
        public double CenterTolerance { get; set; }
        public double RadiusTolerance { get; set; }
    }

    private sealed class LineBaseline
    {
        public BenchmarkCrop Crop { get; set; } = new();
        public string Method { get; set; } = "FitLine";
        public int Threshold { get; set; }
        public double MinLength { get; set; }
        public double MaxGap { get; set; }
        public double ExpectedAngle { get; set; }
        public double AngleTolerance { get; set; }
        public double ExpectedMinLength { get; set; }
        public double ExpectedMaxLength { get; set; }
        public double MaxResidualMean { get; set; }
    }

    private sealed class BenchmarkCrop
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
