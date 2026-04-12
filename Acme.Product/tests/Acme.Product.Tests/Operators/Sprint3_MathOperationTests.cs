using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class Sprint3_MathOperationTests
{
    private readonly MathOperationOperator _operator;

    public Sprint3_MathOperationTests()
    {
        _operator = new MathOperationOperator(Substitute.For<ILogger<MathOperationOperator>>());
    }

    [Theory]
    [InlineData(10, 5, "Add", 15)]
    [InlineData(10, 5, "Subtract", 5)]
    [InlineData(10, 5, "Multiply", 50)]
    [InlineData(10, 5, "Divide", 2)]
    public async Task MathOperation_BasicOperations_ReturnsCorrectResult(
        double a,
        double b,
        string operation,
        double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", a }, { "ValueB", b } });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(-10, "Abs", 10)]
    [InlineData(9, "Sqrt", 3)]
    [InlineData(3.7, "Round", 4)]
    public async Task MathOperation_SingleOperand_ReturnsCorrectResult(
        double a,
        string operation,
        double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", a } });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(10, 5, "Min", 5)]
    [InlineData(10, 5, "Max", 10)]
    [InlineData(2, 3, "Power", 8)]
    [InlineData(17, 5, "Modulo", 2)]
    public async Task MathOperation_AdvancedOperations_ReturnsCorrectResult(
        double a,
        double b,
        string operation,
        double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", a }, { "ValueB", b } });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Fact]
    public async Task MathOperation_DivideByZero_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Divide" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", 10 }, { "ValueB", 0 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("Divisor", result.ErrorMessage!);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public async Task MathOperation_IsPositive_ReturnsCorrectValue(double value, bool expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Add" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", value }, { "ValueB", 0 } });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["IsPositive"]);
    }

    [Fact]
    public void MathOperation_ValidateParameters_InvalidOperation_ReturnsError()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "InvalidOp" } });
        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task MathOperation_MissingValueA_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Add" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueB", 5 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("ValueA", result.ErrorMessage!);
    }

    [Fact]
    public async Task MathOperation_InvalidValueA_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Add" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", "abc" }, { "ValueB", 5 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("ValueA", result.ErrorMessage!);
    }

    [Fact]
    public async Task MathOperation_BinaryMode_MissingValueB_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Multiply" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", 4 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("ValueB", result.ErrorMessage!);
    }

    [Fact]
    public async Task MathOperation_BinaryMode_InvalidValueB_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Power" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", 2 }, { "ValueB", "oops" } });

        Assert.False(result.IsSuccess);
        Assert.Contains("ValueB", result.ErrorMessage!);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task MathOperation_NonFiniteValueA_ReturnsFailure(double nonFinite)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Abs" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "ValueA", nonFinite } });

        Assert.False(result.IsSuccess);
        Assert.Contains("finite", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public async Task MathOperation_NonFiniteValueB_ReturnsFailure(string nonFiniteText)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Add" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "ValueA", 1 },
            { "ValueB", nonFiniteText }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("finite", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MathOperation_NonFiniteResult_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Power" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "ValueA", 1e308 },
            { "ValueB", 2 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("finite", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestMathOperation", OperatorType.MathOperation, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "Operation", "Operation", "Math operation type", "string", "Add", isRequired: true));

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.UpdateParameter(key, value);
            }
        }

        return op;
    }
}
