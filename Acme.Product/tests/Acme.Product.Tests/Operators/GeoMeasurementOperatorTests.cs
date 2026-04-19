using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class GeoMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeGeoMeasurement()
    {
        CreateSut().OperatorType.Should().Be(OperatorType.GeoMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_LineCircle_ShouldReturnBoundaryGap()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Element1Type"] = "Line",
            ["Element2Type"] = "Circle",
            ["DistanceModel"] = "InfiniteLine"
        });

        var inputs = new Dictionary<string, object>
        {
            ["Element1"] = new LineData(0, 0, 100, 0),
            ["Element2"] = new CircleData(50, 20, 5)
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(15.0, 1e-6);
        result.OutputData["DistanceMeaning"].Should().Be("BoundaryGap");
        result.OutputData["Relation"].Should().Be("Separated");
    }

    [Fact]
    public async Task ExecuteAsync_CircleCircle_ShouldDistinguishContainment()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Element1Type"] = "Circle",
            ["Element2Type"] = "Circle"
        });

        var inputs = new Dictionary<string, object>
        {
            ["Element1"] = new CircleData(0, 0, 20),
            ["Element2"] = new CircleData(5, 0, 5)
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(10.0, 1e-6);
        result.OutputData["Relation"].Should().Be("Contained");
    }

    [Fact]
    public async Task ExecuteAsync_LineLine_SegmentModel_ShouldReturnSegmentShortestDistance()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Element1Type"] = "Line",
            ["Element2Type"] = "Line",
            ["DistanceModel"] = "Segment"
        });

        var inputs = new Dictionary<string, object>
        {
            ["Element1"] = new LineData(0, 0, 10, 0),
            ["Element2"] = new LineData(20, 5, 20, 15)
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(Math.Sqrt(125.0), 1e-3);
        result.OutputData["DistanceModel"].Should().Be("Segment");
    }

    [Fact]
    public void ValidateParameters_WithInvalidDistanceModel_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["DistanceModel"] = "Ray" });
        sut.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PointLine_ShouldReuseDedicatedUncertaintyPropagation()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Element1Type"] = "Point",
            ["Element2Type"] = "Line",
            ["DistanceModel"] = "InfiniteLine"
        });

        var point = new Position(20.25, 10.50);
        var line = new LineData(0.25f, 0.25f, 40.25f, 0.25f);
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Element1"] = point,
            ["Element2"] = line
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(10.25, 1e-6);
        var uncertaintyPx = Convert.ToDouble(result.OutputData["UncertaintyPx"]);
        uncertaintyPx.Should().BeGreaterThan(0.01);
        uncertaintyPx.Should().BeLessThan(0.20);
    }

    [Fact]
    public async Task ExecuteAsync_LineLine_ShouldReuseDedicatedUncertaintyPropagation()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Element1Type"] = "Line",
            ["Element2Type"] = "Line",
            ["DistanceModel"] = "InfiniteLine"
        });

        var line1 = new LineData(0.25f, 2.50f, 40.25f, 2.50f);
        var line2 = new LineData(0.25f, 15.00f, 40.25f, 15.00f);
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Element1"] = line1,
            ["Element2"] = line2
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(12.5, 1e-6);
        var uncertaintyPx = Convert.ToDouble(result.OutputData["UncertaintyPx"]);
        uncertaintyPx.Should().BeGreaterThan(0.01);
        uncertaintyPx.Should().BeLessThan(0.20);
    }

    private static GeoMeasurementOperator CreateSut()
    {
        return new GeoMeasurementOperator(Substitute.For<ILogger<GeoMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("GeoMeasurement", OperatorType.GeoMeasurement, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
            }
        }

        return op;
    }
}
