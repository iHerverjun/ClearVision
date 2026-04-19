using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class PointLineDistanceOperatorTests
{
    private readonly PointLineDistanceOperator _operator;

    public PointLineDistanceOperatorTests()
    {
        _operator = new PointLineDistanceOperator(Substitute.For<ILogger<PointLineDistanceOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePointLineDistance()
    {
        _operator.OperatorType.Should().Be(OperatorType.PointLineDistance);
    }

    [Fact]
    public async Task ExecuteAsync_SegmentModel_ShouldClampFootPointToSegment()
    {
        var op = new Operator("p2l", OperatorType.PointLineDistance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("DistanceModel", "Segment", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Point"] = new Position(20, 10),
            ["Line"] = new LineData(0, 0, 10, 0)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(Math.Sqrt(200), 1e-6);
        var foot = result.OutputData["FootPoint"].Should().BeOfType<Position>().Subject;
        foot.X.Should().BeApproximately(10.0, 1e-6);
        foot.Y.Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteLineModel_ShouldUseInfiniteProjection()
    {
        var op = new Operator("p2l", OperatorType.PointLineDistance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("DistanceModel", "InfiniteLine", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Point"] = new Position(20, 10),
            ["Line"] = new LineData(0, 0, 10, 0)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(10.0, 1e-6);
        var foot = result.OutputData["FootPoint"].Should().BeOfType<Position>().Subject;
        foot.X.Should().BeApproximately(20.0, 1e-6);
        foot.Y.Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_SubpixelInputs_ShouldPropagateMeasurementUncertainty()
    {
        var op = new Operator("p2l", OperatorType.PointLineDistance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("DistanceModel", "InfiniteLine", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Point"] = new Position(20.25, 10.50),
            ["Line"] = new LineData(0.25f, 0.25f, 40.25f, 0.25f)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(10.25, 1e-6);
        var uncertaintyPx = Convert.ToDouble(result.OutputData["UncertaintyPx"]);
        uncertaintyPx.Should().BeGreaterThan(0.01);
        uncertaintyPx.Should().BeLessThan(0.20);
        Convert.ToDouble(result.OutputData["Confidence"]).Should().BeGreaterThan(0.8);
    }
}
