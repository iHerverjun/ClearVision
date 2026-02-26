using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

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
        Assert.Equal(OperatorType.LineLineDistance, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithParallelLines_ShouldReturnDistance()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "ParallelThreshold", 2.0 } });
        var inputs = new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 10, 0) },
            { "Line2", new LineData(0, 10, 10, 10) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True((bool)result.OutputData!["IsParallel"]);
        Assert.False((bool)result.OutputData["HasIntersection"]);

        var intersection = Assert.IsType<Position>(result.OutputData["Intersection"]);
        Assert.True(double.IsNaN(intersection.X));
        Assert.True(double.IsNaN(intersection.Y));

        Assert.Equal(10.0, Convert.ToDouble(result.OutputData["Distance"]), 2);
    }

    [Fact]
    public async Task ExecuteAsync_WithCrossingLines_ShouldReturnIntersection()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "ParallelThreshold", 2.0 } });
        var inputs = new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 10, 10) },
            { "Line2", new LineData(0, 10, 10, 0) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.False((bool)result.OutputData!["IsParallel"]);
        Assert.True((bool)result.OutputData["HasIntersection"]);

        var intersection = Assert.IsType<Position>(result.OutputData["Intersection"]);
        Assert.Equal(5.0, intersection.X, 3);
        Assert.Equal(5.0, intersection.Y, 3);
        Assert.Equal(0.0, Convert.ToDouble(result.OutputData["Distance"]), 6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidThreshold_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "ParallelThreshold", 100.0 } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
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
