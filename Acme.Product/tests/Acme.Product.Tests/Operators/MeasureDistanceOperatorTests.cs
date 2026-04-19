using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MeasureDistanceOperatorTests
{
    private readonly MeasureDistanceOperator _operator;

    public MeasureDistanceOperatorTests()
    {
        _operator = new MeasureDistanceOperator(Substitute.For<ILogger<MeasureDistanceOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.Measurement);
    }

    [Fact]
    public async Task ExecuteAsync_PointInputsShouldRespectHorizontalMeasureType()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "Horizontal", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PointA"] = new Point(10, 10),
            ["PointB"] = new Point(25, 30)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(15.0, 1e-6);
        Convert.ToInt32(result.OutputData["Y2"]).Should().Be(10);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeApproximately(Math.Sqrt(0.5), 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_PointInputsShouldPreserveSubpixelPointToPointDistance()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "PointToPoint", "string"));

        var pointA = new Position(10.25, 20.50);
        var pointB = new Position(42.75, 63.125);
        var expected = Math.Sqrt(Math.Pow(pointB.X - pointA.X, 2) + Math.Pow(pointB.Y - pointA.Y, 2));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PointA"] = pointA,
            ["PointB"] = pointB
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(expected, 1e-9);
        Convert.ToDouble(result.OutputData["X1"]).Should().BeApproximately(pointA.X, 1e-9);
        Convert.ToDouble(result.OutputData["Y2"]).Should().BeApproximately(pointB.Y, 1e-9);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.08);
    }

    [Fact]
    public async Task ExecuteAsync_PointInputsShouldPreserveSubpixelHorizontalDistance()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "Horizontal", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PointA"] = new Point2d(11.25, 18.75),
            ["PointB"] = new Point2d(30.90, 47.50)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(19.65, 1e-9);
        Convert.ToDouble(result.OutputData["Y2"]).Should().BeApproximately(18.75, 1e-9);
        Convert.ToDouble(result.OutputData["DeltaY"]).Should().BeApproximately(0.0, 1e-9);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.08);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        (await _operator.ExecuteAsync(op, null)).IsSuccess.Should().BeFalse();
    }
}
