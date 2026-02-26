using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class StatisticsOperatorTests
{
    private readonly StatisticsOperator _operator;

    public StatisticsOperatorTests()
    {
        _operator = new StatisticsOperator(Substitute.For<ILogger<StatisticsOperator>>());
    }

    [Fact]
    public async Task ExecuteAsync_BasicStats_ReturnsCorrectResults()
    {
        var op = CreateOperator(Guid.NewGuid());
        await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 20.0 } });

        Assert.True(result.IsSuccess);
        Assert.Equal(15.0, (double)result.OutputData!["Mean"]);
        Assert.Equal(2, (int)result.OutputData["Count"]);
        Assert.Equal(Math.Sqrt(50), (double)result.OutputData["StdDev"], 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCpkParams_CalculatesCpkCorrectly()
    {
        var op = CreateOperator(
            Guid.NewGuid(),
            new Dictionary<string, object>
            {
                { "USL", 13.0 },
                { "LSL", 7.0 }
            });

        var offset = Math.Sqrt(0.5);
        await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 - offset } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 + offset } });

        Assert.True(result.IsSuccess);
        Assert.Equal(1.0, (double)result.OutputData!["Cp"], 2);
        Assert.Equal(1.0, (double)result.OutputData["Cpk"], 2);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentOperators_ShouldKeepIndependentHistory()
    {
        var opA = CreateOperator(Guid.NewGuid());
        var opB = CreateOperator(Guid.NewGuid());

        await _operator.ExecuteAsync(opA, new Dictionary<string, object> { { "Value", 10.0 } });
        var resultB = await _operator.ExecuteAsync(opB, new Dictionary<string, object> { { "Value", 20.0 } });
        var resultA = await _operator.ExecuteAsync(opA, new Dictionary<string, object> { { "Value", 30.0 } });

        Assert.Equal(1, (int)resultB.OutputData!["Count"]);
        Assert.Equal(2, (int)resultA.OutputData!["Count"]);
    }

    [Fact]
    public void ValidateParameters_UslLessThanLsl_ReturnsInvalid()
    {
        var op = CreateOperator(
            Guid.NewGuid(),
            new Dictionary<string, object>
            {
                { "USL", 5.0 },
                { "LSL", 10.0 }
            });

        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    private static Operator CreateOperator(Guid id, Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(id, "Stats", OperatorType.Statistics, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "WindowSize", "WindowSize", "", "int", 1000));
        op.AddParameter(new Parameter(Guid.NewGuid(), "Reset", "Reset", "", "bool", false));

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, "", "double", value));
            }
        }

        return op;
    }
}
