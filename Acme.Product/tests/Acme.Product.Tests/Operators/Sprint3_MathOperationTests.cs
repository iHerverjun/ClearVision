// Sprint3_MathOperationTests.cs
// Sprint 3 Task 3.1 MathOperation 算子单元测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

/// <summary>
/// Sprint 3 Task 3.1: MathOperation 算子单元测试
/// </summary>
public class Sprint3_MathOperationTests
{
    private readonly ILogger<MathOperationOperator> _loggerMock;
    private readonly MathOperationOperator _operator;

    public Sprint3_MathOperationTests()
    {
        _loggerMock = Substitute.For<ILogger<MathOperationOperator>>();
        _operator = new MathOperationOperator(_loggerMock);
    }

    [Theory]
    [InlineData(10, 5, "Add", 15)]
    [InlineData(10, 5, "Subtract", 5)]
    [InlineData(10, 5, "Multiply", 50)]
    [InlineData(10, 5, "Divide", 2)]
    public async Task MathOperation_BasicOperations_ReturnsCorrectResult(
        double a, double b, string operation, double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var inputs = new Dictionary<string, object> { { "ValueA", a }, { "ValueB", b } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(-10, "Abs", 10)]
    [InlineData(9, "Sqrt", 3)]
    [InlineData(3.7, "Round", 4)]
    public async Task MathOperation_SingleOperand_ReturnsCorrectResult(
        double a, string operation, double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var inputs = new Dictionary<string, object> { { "ValueA", a } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(10, 5, "Min", 5)]
    [InlineData(10, 5, "Max", 10)]
    [InlineData(2, 3, "Power", 8)]
    [InlineData(17, 5, "Modulo", 2)]
    public async Task MathOperation_AdvancedOperations_ReturnsCorrectResult(
        double a, double b, string operation, double expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", operation } });
        var inputs = new Dictionary<string, object> { { "ValueA", a }, { "ValueB", b } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Fact]
    public async Task MathOperation_DivideByZero_ReturnsFailure()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Divide" } });
        var inputs = new Dictionary<string, object> { { "ValueA", 10 }, { "ValueB", 0 } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
        Assert.Contains("除数", result.ErrorMessage!);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public async Task MathOperation_IsPositive_ReturnsCorrectValue(double value, bool expected)
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "Add" } });
        var inputs = new Dictionary<string, object> { { "ValueA", value }, { "ValueB", 0 } };

        var result = await _operator.ExecuteAsync(op, inputs);

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

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestMathOperation", OperatorType.MathOperation, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Operation", "操作", "数学操作类型", "string", "Add", isRequired: true));

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
