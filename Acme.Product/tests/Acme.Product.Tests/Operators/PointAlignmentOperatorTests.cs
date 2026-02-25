using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class PointAlignmentOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePointAlignment()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.PointAlignment, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithMillimeterOutput_ShouldApplyPixelSize()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "OutputUnit", "mm" },
            { "PixelSize", 0.5 }
        });

        var inputs = new Dictionary<string, object>
        {
            { "CurrentPoint", new Position(14, 9) },
            { "ReferencePoint", new Position(10, 5) }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(2.0, Convert.ToDouble(result.OutputData!["OffsetX"]), 6);
        Assert.Equal(2.0, Convert.ToDouble(result.OutputData["OffsetY"]), 6);
        Assert.Equal(Math.Sqrt(8), Convert.ToDouble(result.OutputData["Distance"]), 6);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPoints_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>());

        Assert.False(result.IsSuccess);
        Assert.Contains("CurrentPoint", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ValidateParameters_WithInvalidUnit_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "OutputUnit", "cm" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static PointAlignmentOperator CreateSut()
    {
        return new PointAlignmentOperator(Substitute.For<ILogger<PointAlignmentOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PointAlignment", OperatorType.PointAlignment, 0, 0);

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
