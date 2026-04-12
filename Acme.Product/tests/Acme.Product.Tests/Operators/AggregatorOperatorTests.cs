using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class AggregatorOperatorTests
{
    private readonly AggregatorOperator _operator;

    public AggregatorOperatorTests()
    {
        _operator = new AggregatorOperator(Substitute.For<ILogger<AggregatorOperator>>());
    }

    [Fact]
    public async Task ExecuteAsync_MergeMode_WithMixedValues_ShouldReturnMergedListAndNumericCount()
    {
        var op = CreateOperator("Merge");
        var inputs = new Dictionary<string, object>
        {
            ["Value1"] = 2,
            ["Value2"] = "text",
            ["Value3"] = "NaN"
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.IsType<List<object>>(result.OutputData!["Result"]);
        Assert.Equal(1, Convert.ToInt32(result.OutputData["NumericCount"]));
        Assert.Equal(2d, Convert.ToDouble(result.OutputData["MaxValue"]));
        Assert.Equal(2d, Convert.ToDouble(result.OutputData["MinValue"]));
        Assert.Equal(2d, Convert.ToDouble(result.OutputData["Average"]));
    }

    [Fact]
    public async Task ExecuteAsync_AverageMode_WithNoNumericInput_ShouldFail()
    {
        var op = CreateOperator("Average");
        var inputs = new Dictionary<string, object>
        {
            ["Value1"] = "abc",
            ["Value2"] = "Infinity"
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
        Assert.Contains("finite numeric input", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AverageMode_WithFiniteValues_ShouldReturnAverageAndNumericCount()
    {
        var op = CreateOperator("Average");
        var inputs = new Dictionary<string, object>
        {
            ["Value1"] = 3,
            ["Value2"] = "9",
            ["Value3"] = "Infinity"
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Convert.ToInt32(result.OutputData!["NumericCount"]));
        Assert.Equal(6d, Convert.ToDouble(result.OutputData["Result"]));
        Assert.Equal(9d, Convert.ToDouble(result.OutputData["MaxValue"]));
        Assert.Equal(3d, Convert.ToDouble(result.OutputData["MinValue"]));
        Assert.Equal(6d, Convert.ToDouble(result.OutputData["Average"]));
    }

    [Fact]
    public async Task ExecuteAsync_MaxMode_WithNoInputs_ShouldFail()
    {
        var op = CreateOperator("Max");

        var result = await _operator.ExecuteAsync(op, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("finite numeric input", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_InvalidMode_ShouldFail()
    {
        var op = CreateOperator("Unknown");
        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMode_ShouldFailAtRuntime()
    {
        var op = CreateOperator("Unknown");

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { ["Value1"] = 1 });

        Assert.False(result.IsSuccess);
        Assert.Contains("Mode", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static Operator CreateOperator(string mode)
    {
        var op = new Operator("agg", OperatorType.Aggregator, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "Mode", "Mode", "Aggregator mode", "string", mode, isRequired: true));
        return op;
    }
}
