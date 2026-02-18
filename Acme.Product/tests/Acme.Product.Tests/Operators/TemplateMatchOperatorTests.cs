// TemplateMatchOperatorTests.cs
// TemplateMatchOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class TemplateMatchOperatorTests
{
    private readonly TemplateMatchOperator _operator;

    public TemplateMatchOperatorTests()
    {
        _operator = new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTemplateMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.TemplateMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.TemplateMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.TemplateMatching, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.TemplateMatching, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
