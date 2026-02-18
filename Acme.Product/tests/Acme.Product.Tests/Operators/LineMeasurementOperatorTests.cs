// LineMeasurementOperatorTests.cs
// LineMeasurementOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class LineMeasurementOperatorTests
{
    private readonly LineMeasurementOperator _operator;

    public LineMeasurementOperatorTests()
    {
        _operator = new LineMeasurementOperator(Substitute.For<ILogger<LineMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeLineMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.LineMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.LineMeasurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.LineMeasurement, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.LineMeasurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
