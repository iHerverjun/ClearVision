// ShapeMatchingOperatorTests.cs
// ShapeMatchingOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ShapeMatchingOperatorTests
{
    private readonly ShapeMatchingOperator _operator;

    public ShapeMatchingOperatorTests()
    {
        var logger = Substitute.For<ILogger<ShapeMatchingOperator>>();
        _operator = new ShapeMatchingOperator(logger);
    }

    [Fact]
    public void OperatorType_ShouldBeShapeMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.ShapeMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }
}
