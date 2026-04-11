using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class AngleMeasurementOperatorTests
{
    private readonly AngleMeasurementOperator _operator;

    public AngleMeasurementOperatorTests()
    {
        _operator = new AngleMeasurementOperator(Substitute.For<ILogger<AngleMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeAngleMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.AngleMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.AngleMeasurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.AngleMeasurement, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
        result.OutputData.Should().ContainKey("StatusCode");
        result.OutputData!["StatusCode"].Should().Be("OK");
    }

    [Fact]
    public async Task ExecuteAsync_WithDegenerateArm_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.AngleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Point1X", 50, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Point1Y", 50, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Point2X", 50, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Point2Y", 50, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Point3X", 120, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Point3Y", 50, "int"));

        using var image = TestHelpers.CreateTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DegenerateGeometry");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.AngleMeasurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
