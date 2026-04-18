// Sprint3_LogicGateTests.cs
// Sprint 3 Task 3.2 LogicGate 算子单元测试
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
/// Sprint 3 Task 3.2: LogicGate 算子单元测试
/// </summary>
public class Sprint3_LogicGateTests
{
    private readonly ILogger<LogicGateOperator> _loggerMock;
    private readonly LogicGateOperator _operator;

    public Sprint3_LogicGateTests()
    {
        _loggerMock = Substitute.For<ILogger<LogicGateOperator>>();
        _operator = new LogicGateOperator(_loggerMock);
    }

    [Theory]
    [InlineData(true, true, "AND", true)]
    [InlineData(true, false, "AND", false)]
    [InlineData(false, true, "AND", false)]
    [InlineData(false, false, "AND", false)]
    public async Task LogicGate_AND_ReturnsCorrectResult(bool a, bool b, string op, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", op } });
        var inputs = new Dictionary<string, object> { { "InputA", a }, { "InputB", b } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(true, true, "OR", true)]
    [InlineData(true, false, "OR", true)]
    [InlineData(false, true, "OR", true)]
    [InlineData(false, false, "OR", false)]
    public async Task LogicGate_OR_ReturnsCorrectResult(bool a, bool b, string op, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", op } });
        var inputs = new Dictionary<string, object> { { "InputA", a }, { "InputB", b } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LogicGate_NOT_ReturnsCorrectResult(bool input, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", "NOT" } });
        var inputs = new Dictionary<string, object> { { "InputA", input } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(true, true, "XOR", false)]
    [InlineData(true, false, "XOR", true)]
    [InlineData(false, true, "XOR", true)]
    [InlineData(false, false, "XOR", false)]
    public async Task LogicGate_XOR_ReturnsCorrectResult(bool a, bool b, string op, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", op } });
        var inputs = new Dictionary<string, object> { { "InputA", a }, { "InputB", b } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(true, true, "NAND", false)]
    [InlineData(true, false, "NAND", true)]
    [InlineData(false, false, "NAND", true)]
    public async Task LogicGate_NAND_ReturnsCorrectResult(bool a, bool b, string op, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", op } });
        var inputs = new Dictionary<string, object> { { "InputA", a }, { "InputB", b } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Theory]
    [InlineData(1, 1, "AND", true)]  // 1 AND 1 = true
    [InlineData(1, 0, "AND", false)] // 1 AND 0 = false
    [InlineData("true", "false", "AND", false)]
    public async Task LogicGate_NonBooleanInputs_ConvertsCorrectly(object a, object b, string op, bool expected)
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", op } });
        var inputs = new Dictionary<string, object> { { "InputA", a }, { "InputB", b } };

        var result = await _operator.ExecuteAsync(oper, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["Result"]);
    }

    [Fact]
    public void LogicGate_ValidateParameters_InvalidOperation_ReturnsError()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Operation", "INVALID" } });
        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task LogicGate_WithMissingBinaryInput_ShouldFailClosed()
    {
        var oper = CreateOperator(new Dictionary<string, object> { { "Operation", "AND" } });
        var result = await _operator.ExecuteAsync(oper, new Dictionary<string, object> { { "InputA", true } });

        Assert.False(result.IsSuccess);
        Assert.Contains("InputB", result.ErrorMessage ?? string.Empty);
    }

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestLogicGate", OperatorType.LogicGate, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Operation", "操作", "AND/OR/NOT/XOR/NAND/NOR", "string", "AND", isRequired: true));

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
