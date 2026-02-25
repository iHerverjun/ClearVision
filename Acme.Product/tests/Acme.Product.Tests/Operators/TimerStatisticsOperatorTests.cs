using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class TimerStatisticsOperatorTests
{
    private readonly TimerStatisticsOperator _operator;

    public TimerStatisticsOperatorTests()
    {
        _operator = new TimerStatisticsOperator(Substitute.For<ILogger<TimerStatisticsOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTimerStatistics()
    {
        Assert.Equal(OperatorType.TimerStatistics, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithCumulativeMode_ShouldAccumulate()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Cumulative" },
            { "ResetInterval", 0 }
        });

        await _operator.ExecuteAsync(op, null);
        await Task.Delay(25);
        var result = await _operator.ExecuteAsync(op, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(2, (int)result.OutputData!["Count"]);
        Assert.True(Convert.ToDouble(result.OutputData["TotalMs"]) >= Convert.ToDouble(result.OutputData["AverageMs"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "Invalid" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Timer", OperatorType.TimerStatistics, 0, 0);

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
