using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class CaliperToolOperatorTests
{
    private readonly CaliperToolOperator _operator;

    public CaliperToolOperatorTests()
    {
        _operator = new CaliperToolOperator(Substitute.For<ILogger<CaliperToolOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeCaliperTool()
    {
        Assert.Equal(OperatorType.CaliperTool, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleEdgeImage_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", false }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
        Assert.True(result.OutputData.ContainsKey("StatusCode"));
    }

    [Fact]
    public async Task ExecuteAsync_WithSubpixelEnabled_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", true }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
    }

    [Fact]
    public async Task ExecuteAsync_WithSubpixelZernike_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", true },
            { "SubPixelMode", "zernike" }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
    }

    [Fact]
    public async Task ExecuteAsync_WithEdgePairsMode_ShouldEmitPairStatistics()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "MeasureMode", "edge_pairs" },
            { "PairDirection", "any" },
            { "SubpixelAccuracy", false }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("PairDistances"));
        Assert.True(result.OutputData.ContainsKey("AverageDistance"));
        Assert.True(result.OutputData.ContainsKey("DistanceStdDev"));

        var distances = Assert.IsType<List<double>>(result.OutputData["PairDistances"]);
        Assert.Single(distances);
        Assert.InRange(distances[0], 35.0, 45.0);

        var avg = Convert.ToDouble(result.OutputData["AverageDistance"]);
        Assert.InRange(avg, 35.0, 45.0);

        var stdDev = Convert.ToDouble(result.OutputData["DistanceStdDev"]);
        Assert.InRange(stdDev, 0.0, 1e-6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidDirection_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Direction", "Diagonal" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_WithIndustrialSubpixelStripe_ShouldMeetIndustrialTolerance()
    {
        const double expectedWidth = 40.0;
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "PairDirection", "positive_to_negative" },
            { "EdgeThreshold", 6.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", true }
        });

        using var image = IndustrialMeasurementSceneFactory.CreateFilledVerticalStripeImage(
            width: 240,
            height: 120,
            leftX: 90.0,
            rightX: 130.0,
            topY: 12,
            bottomY: 108);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["SearchRegion"] = new Rect(60, 8, 120, 104);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var width = Convert.ToDouble(result.OutputData!["Width"]);
        var uncertainty = Convert.ToDouble(result.OutputData["UncertaintyPx"]);
        var distances = Assert.IsType<List<double>>(result.OutputData["PairDistances"]);
        var pairUncertainties = Assert.IsType<List<double>>(result.OutputData["PairUncertainties"]);

        width.Should().BeApproximately(expectedWidth, 0.15);
        distances.Should().ContainSingle();
        distances[0].Should().BeApproximately(expectedWidth, 0.15);
        pairUncertainties.Should().ContainSingle();
        pairUncertainties[0].Should().BeLessThan(0.15);
        uncertainty.Should().BeLessThan(0.15);
        Convert.ToDouble(result.OutputData["SamplePitchPx"]).Should().BeLessThan(0.2);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Caliper", OperatorType.CaliperTool, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateCaliperImage()
    {
        var mat = new Mat(120, 220, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(90, 10, 40, 100), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
