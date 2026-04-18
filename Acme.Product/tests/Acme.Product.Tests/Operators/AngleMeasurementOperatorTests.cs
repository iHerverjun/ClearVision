using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
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
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
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
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
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
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSubpixelPointInputs_ShouldReachIndustrialPrecision()
    {
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Unit", "Degree", "string"));
        using var image = TestHelpers.CreateTestImage(width: 160, height: 120);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Point1"] = new Position(0.25, 0.25);
        inputs["Point2"] = new Position(10.25, 10.25);
        inputs["Point3"] = new Position(20.25, 10.25);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Angle"]).Should().BeApproximately(135.0, 1e-6);
        result.OutputData["InputMode"].Should().Be("ThreePointsInput");
        Convert.ToDouble(result.OutputData["UncertaintyDeg"]).Should().BeLessThan(0.6);
    }

    [Fact]
    public async Task ExecuteAsync_WithLineInputs_ShouldPreserveSubpixelLineAngle()
    {
        var op = new Operator("angle", OperatorType.AngleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Unit", "Degree", "string"));
        using var image = TestHelpers.CreateTestImage(width: 160, height: 120);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(20.25f, 50.0f, 80.25f, 50.0f);
        inputs["Line2"] = new LineData(50.5f, 50.0f, 80.5f, 80.0f);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Angle"]).Should().BeApproximately(45.0, 1e-6);
        result.OutputData["InputMode"].Should().Be("TwoLines");
        result.OutputData["Vertex"].Should().BeOfType<Position>();
        Convert.ToDouble(result.OutputData["UncertaintyDeg"]).Should().BeLessThan(0.25);
    }
}
