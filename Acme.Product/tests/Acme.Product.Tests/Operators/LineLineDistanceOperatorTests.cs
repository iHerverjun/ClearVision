using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class LineLineDistanceOperatorTests
{
    private readonly LineLineDistanceOperator _operator;

    public LineLineDistanceOperatorTests()
    {
        _operator = new LineLineDistanceOperator(Substitute.For<ILogger<LineLineDistanceOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeLineLineDistance()
    {
        _operator.OperatorType.Should().Be(OperatorType.LineLineDistance);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultSegmentModel_ShouldReturnSegmentShortestDistance()
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object>
        {
            ["Line1"] = new LineData(0, 0, 10, 0),
            ["Line2"] = new LineData(20, 5, 20, 15)
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["DistanceModel"].Should().Be("Segment");
        Convert.ToDouble(result.OutputData["Distance"]).Should().BeApproximately(Math.Sqrt(125.0), 1e-3);
        result.OutputData["HasIntersection"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteLineModel_ShouldReturnZeroWhenInfiniteLinesIntersect()
    {
        var op = CreateOperator(new Dictionary<string, object> { ["DistanceModel"] = "InfiniteLine" });
        var inputs = new Dictionary<string, object>
        {
            ["Line1"] = new LineData(0, 0, 10, 0),
            ["Line2"] = new LineData(20, 5, 20, 15)
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["DistanceModel"].Should().Be("InfiniteLine");
        Convert.ToDouble(result.OutputData["Distance"]).Should().BeApproximately(0.0, 1e-6);
        result.OutputData["HasIntersection"].Should().Be(true);
        var cross = result.OutputData["Intersection"].Should().BeOfType<Position>().Subject;
        cross.X.Should().BeApproximately(20.0, 1e-6);
        cross.Y.Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidThreshold_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { ["ParallelThreshold"] = 100.0 });
        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("L2L", OperatorType.LineLineDistance, 0, 0);
        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
