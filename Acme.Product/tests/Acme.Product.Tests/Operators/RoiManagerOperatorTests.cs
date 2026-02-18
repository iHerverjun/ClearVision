// RoiManagerOperatorTests.cs
// RoiManagerOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class RoiManagerOperatorTests
{
    private readonly RoiManagerOperator _operator;

    public RoiManagerOperatorTests()
    {
        var logger = Substitute.For<ILogger<RoiManagerOperator>>();
        _operator = new RoiManagerOperator(logger);
    }

    [Fact]
    public void OperatorType_ShouldBeRoiManager()
    {
        _operator.OperatorType.Should().Be(OperatorType.RoiManager);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.RoiManager, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.RoiManager, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }
}
