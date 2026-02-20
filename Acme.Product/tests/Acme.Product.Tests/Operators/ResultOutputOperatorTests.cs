// ResultOutputOperatorTests.cs
// ResultOutputOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ResultOutputOperatorTests
{
    private readonly ResultOutputOperator _operator;

    public ResultOutputOperatorTests()
    {
        _operator = new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeResultOutput()
    {
        _operator.OperatorType.Should().Be(OperatorType.ResultOutput);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.ResultOutput, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.ResultOutput, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.ResultOutput, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
