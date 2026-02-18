// GeometricFittingOperatorTests.cs
// GeometricFittingOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class GeometricFittingOperatorTests
{
    private readonly GeometricFittingOperator _operator;

    public GeometricFittingOperatorTests()
    {
        var logger = Substitute.For<ILogger<GeometricFittingOperator>>();
        _operator = new GeometricFittingOperator(logger);
    }

    [Fact]
    public void OperatorType_ShouldBeGeometricFitting()
    {
        _operator.OperatorType.Should().Be(OperatorType.GeometricFitting);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.GeometricFitting, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.GeometricFitting, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }
}
