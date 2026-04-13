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
        Assert.Equal(OperatorType.EdgeIntersection, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithCrossLines_ShouldReturnIntersection()
    {
        var result = await CreateSut().ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 10, 10) },
            { "Line2", new LineData(0, 10, 10, 0) }
        });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["HasIntersection"]);
        Assert.True((bool)result.OutputData["SegmentsIntersect"]);
        var point = Assert.IsType<Position>(result.OutputData["Point"]);
        Assert.Equal(5.0, point.X, 3);
        Assert.Equal(5.0, point.Y, 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithDisjointSegmentsInSegmentMode_ShouldExposeInfiniteLineButRejectSegmentHit()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "IntersectionMode", "SegmentOnly" } });
        var result = await CreateSut().ExecuteAsync(op, new Dictionary<string, object>
        {
            { "Line1", new LineData(0, 0, 1, 0) },
            { "Line2", new LineData(2, -1, 2, 1) }
        });

        Assert.True(result.IsSuccess);
        Assert.False((bool)result.OutputData!["HasIntersection"]);
        Assert.False((bool)result.OutputData["SegmentsIntersect"]);
        var point = Assert.IsType<Position>(result.OutputData["Point"]);
        Assert.Equal(2.0, point.X, 3);
        Assert.Equal(0.0, point.Y, 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithDegenerateLine_ShouldFail()
    {
        var result = await CreateSut().ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            { "Line1", new LineData(1, 1, 1, 1) },
            { "Line2", new LineData(0, 10, 10, 0) }
        });

        Assert.False(result.IsSuccess);
    }

    private static EdgeIntersectionOperator CreateSut() => new(Substitute.For<ILogger<EdgeIntersectionOperator>>());

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("EdgeIntersection", OperatorType.EdgeIntersection, 0, 0);
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
