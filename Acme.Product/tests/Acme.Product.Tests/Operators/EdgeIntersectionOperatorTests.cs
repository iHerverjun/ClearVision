using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class EdgeIntersectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeEdgeIntersection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.EdgeIntersection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithCrossLines_ShouldReturnIntersection()
    {
        var sut = CreateSut();
        var op = CreateOperator();
        var inputs = new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 10, 10) },
            { "Line2", new LineData(0, 10, 10, 0) }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True((bool)result.OutputData!["HasIntersection"]);
        var point = Assert.IsType<Position>(result.OutputData["Point"]);
        Assert.Equal(5.0, point.X, 3);
        Assert.Equal(5.0, point.Y, 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithParallelLines_ShouldReturnNoIntersection()
    {
        var sut = CreateSut();
        var op = CreateOperator();
        var inputs = new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 10, 0) },
            { "Line2", new LineData(0, 5, 10, 5) }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.False((bool)result.OutputData!["HasIntersection"]);
    }

    private static EdgeIntersectionOperator CreateSut()
    {
        return new EdgeIntersectionOperator(Substitute.For<ILogger<EdgeIntersectionOperator>>());
    }

    private static Operator CreateOperator()
    {
        return new Operator("EdgeIntersection", OperatorType.EdgeIntersection, 0, 0);
    }
}

