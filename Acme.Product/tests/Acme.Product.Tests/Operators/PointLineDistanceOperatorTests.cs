using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

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
        Assert.Equal(OperatorType.PointLineDistance, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInputs_ShouldReturnDistanceAndFootPoint()
    {
        var op = new Operator("P2L", OperatorType.PointLineDistance, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            { "Point", new Position(3, 4) },
            { "Line", new LineData(0, 0, 10, 0) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(4.0, Convert.ToDouble(result.OutputData!["Distance"]), 3);

        var footPoint = Assert.IsType<Position>(result.OutputData["FootPoint"]);
        Assert.Equal(3.0, footPoint.X, 3);
        Assert.Equal(0.0, footPoint.Y, 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingLine_ShouldReturnFailure()
    {
        var op = new Operator("P2L", OperatorType.PointLineDistance, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            { "Point", new Position(3, 4) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }
}
