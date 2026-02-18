// SubpixelEdgeDetectionOperatorTests.cs
// SubpixelEdgeDetectionOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class SubpixelEdgeDetectionOperatorTests
{
    private readonly SubpixelEdgeDetectionOperator _operator;

    public SubpixelEdgeDetectionOperatorTests()
    {
        var logger = Substitute.For<ILogger<SubpixelEdgeDetectionOperator>>();
        _operator = new SubpixelEdgeDetectionOperator(logger);
    }

    [Fact]
    public void OperatorType_ShouldBeSubpixelEdgeDetection()
    {
        _operator.OperatorType.Should().Be(OperatorType.SubpixelEdgeDetection);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.SubpixelEdgeDetection, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.SubpixelEdgeDetection, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }
}
