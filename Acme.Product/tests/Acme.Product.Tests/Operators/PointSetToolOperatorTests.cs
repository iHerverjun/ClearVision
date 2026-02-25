using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class PointSetToolOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePointSetTool()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.PointSetTool, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_Merge_ShouldReturnCombinedCount()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Merge" } });

        var inputs = new Dictionary<string, object>
        {
            { "Points1", new List<Position> { new(0, 0), new(1, 1) } },
            { "Points2", new List<Position> { new(2, 2), new(3, 3) } }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(4, Convert.ToInt32(result.OutputData!["Count"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidOperation_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Cluster" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static PointSetToolOperator CreateSut()
    {
        return new PointSetToolOperator(Substitute.For<ILogger<PointSetToolOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PointSetTool", OperatorType.PointSetTool, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }
}

