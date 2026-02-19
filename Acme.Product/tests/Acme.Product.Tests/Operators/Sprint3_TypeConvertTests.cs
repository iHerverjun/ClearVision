// Sprint3_TypeConvertTests.cs
// Sprint 3 Task 3.3 TypeConvert 算子单元测试
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
/// Sprint 3 Task 3.3: TypeConvert 算子单元测试
/// </summary>
public class Sprint3_TypeConvertTests
{
    private readonly ILogger<TypeConvertOperator> _loggerMock;
    private readonly TypeConvertOperator _operator;

    public Sprint3_TypeConvertTests()
    {
        _loggerMock = Substitute.For<ILogger<TypeConvertOperator>>();
        _operator = new TypeConvertOperator(_loggerMock);
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(3.14, "3.14")]
    [InlineData(true, "True")]
    public async Task TypeConvert_ToString_ReturnsCorrectString(object value, string expected)
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Value", value } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["AsString"]);
    }

    [Theory]
    [InlineData("42", 42f)]
    [InlineData("3.14", 3.14f)]
    [InlineData(100, 100f)]
    [InlineData(true, 1f)]
    public async Task TypeConvert_ToFloat_ReturnsCorrectFloat(object value, float expected)
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Value", value } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["AsFloat"]);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("100", 100)]
    [InlineData(3.9, 3)]
    [InlineData(true, 1)]
    public async Task TypeConvert_ToInteger_ReturnsCorrectInt(object value, int expected)
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Value", value } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["AsInteger"]);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData("", false)]
    [InlineData("hello", true)]
    public async Task TypeConvert_ToBoolean_ReturnsCorrectBool(object value, bool expected)
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Value", value } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.OutputData!["AsBoolean"]);
    }

    [Fact]
    public async Task TypeConvert_NumberWithFormat_ReturnsFormattedString()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Format", "F2" } });
        var inputs = new Dictionary<string, object> { { "Value", 3.14159 } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal("3.14", result.OutputData!["AsString"]);
    }

    [Fact]
    public async Task TypeConvert_EmptyInput_ReturnsFailure()
    {
        var op = CreateOperator();
        var inputs = new Dictionary<string, object>();

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestTypeConvert", OperatorType.TypeConvert, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Format", "格式", "字符串格式", "string", "", isRequired: false));

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
