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
    public async Task ExecuteAsync_ValueNaN_ShouldFail()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "mm" },
            { "ToUnit", "um" },
            { "Scale", 1.0 },
            { "UseCalibration", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", double.NaN } });

        Assert.False(result.IsSuccess);
        Assert.Contains("finite", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ScaleNaN_ShouldFailEvenWhenNoPixelConversion()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "mm" },
            { "ToUnit", "um" },
            { "Scale", "NaN" },
            { "UseCalibration", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 1.0 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("Scale", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_UseCalibrationWithoutPixelSize_ShouldFail()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "Pixel" },
            { "ToUnit", "mm" },
            { "Scale", 0.01 },
            { "UseCalibration", true }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 50.0 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("PixelSize", result.ErrorMessage!);
    }

    [Fact]
    public async Task ExecuteAsync_UseCalibrationWithNonFinitePixelSize_ShouldFail()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "Pixel" },
            { "ToUnit", "mm" },
            { "Scale", 0.01 },
            { "UseCalibration", true }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "Value", 50.0 },
            { "PixelSize", "Infinity" }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("PixelSize", result.ErrorMessage!);
    }

    [Fact]
    public async Task ExecuteAsync_BoundarySmallScale_ShouldSucceed()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "Pixel" },
            { "ToUnit", "mm" },
            { "Scale", 1e-9 },
            { "UseCalibration", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 2.0 } });

        Assert.True(result.IsSuccess);
        Assert.Equal(2e-9, Convert.ToDouble(result.OutputData!["Result"]), 12);
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

    [Fact]
    public void ValidateParameters_WithNonFiniteScale_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FromUnit", "Pixel" },
            { "ToUnit", "mm" },
            { "Scale", "Infinity" }
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
