using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class UnitConvertOperatorTests
{
    private readonly UnitConvertOperator _operator;

    public UnitConvertOperatorTests()
    {
        _operator = new UnitConvertOperator(Substitute.For<ILogger<UnitConvertOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeUnitConvert()
    {
        Assert.Equal(OperatorType.UnitConvert, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_PixelToMillimeter_WithManualScale_ShouldReturnExpectedValue()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "Pixel" },
            { "ToUnit", "mm" },
            { "Scale", 0.02 },
            { "UseCalibration", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 100.0 } });

        Assert.True(result.IsSuccess);
        Assert.Equal(2.0, Convert.ToDouble(result.OutputData!["Result"]), 6);
        Assert.Equal("mm", result.OutputData["Unit"]);
    }

    [Fact]
    public async Task ExecuteAsync_MmToUm_ShouldReturnExpectedValue()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "mm" },
            { "ToUnit", "um" },
            { "Scale", 1.0 },
            { "UseCalibration", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 1.0 } });

        Assert.True(result.IsSuccess);
        Assert.Equal(1000.0, Convert.ToDouble(result.OutputData!["Result"]), 6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidUnit_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "meter" },
            { "ToUnit", "mm" }
        });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("UnitConvert", OperatorType.UnitConvert, 0, 0);

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
