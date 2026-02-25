using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class GapMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeGapMeasurement()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.GapMeasurement, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithPointList_ShouldReturnGapStatistics()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Direction", "Horizontal" } });
        var points = new List<Position>
        {
            new(10, 20),
            new(20, 20),
            new(35, 20),
            new(50, 20)
        };

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { { "Points", points } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);

        var gaps = Assert.IsType<List<double>>(result.OutputData!["Gaps"]);
        Assert.Equal(3, gaps.Count);
        Assert.Equal(10.0, gaps[0], 6);
        Assert.Equal(15.0, gaps[1], 6);
        Assert.Equal(15.0, gaps[2], 6);
        Assert.Equal(13.333333, Convert.ToDouble(result.OutputData["MeanGap"]), 3);
        Assert.Equal(3, Convert.ToInt32(result.OutputData["Count"]));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImageOrPoints_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Image or Points", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ValidateParameters_WithInvalidRange_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinGap", 20.0 },
            { "MaxGap", 10.0 }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static GapMeasurementOperator CreateSut()
    {
        return new GapMeasurementOperator(Substitute.For<ILogger<GapMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("GapMeasurement", OperatorType.GapMeasurement, 0, 0);

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
