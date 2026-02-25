using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class GeoMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeGeoMeasurement()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.GeoMeasurement, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_PointPoint_ShouldReturnDistance()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Element1Type", "Point" },
            { "Element2Type", "Point" }
        });

        var inputs = new Dictionary<string, object>
        {
            { "Element1", new Position(0, 0) },
            { "Element2", new Position(3, 4) }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(5.0, Convert.ToDouble(result.OutputData!["Distance"]), 6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidType_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Element1Type", "Ellipse" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static GeoMeasurementOperator CreateSut()
    {
        return new GeoMeasurementOperator(Substitute.For<ILogger<GeoMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("GeoMeasurement", OperatorType.GeoMeasurement, 0, 0);
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

