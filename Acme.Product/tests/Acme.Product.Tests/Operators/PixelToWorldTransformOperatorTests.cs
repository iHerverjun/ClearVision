using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class PixelToWorldTransformOperatorTests
{
    private readonly PixelToWorldTransformOperator _operator;

    public PixelToWorldTransformOperatorTests()
    {
        _operator = new PixelToWorldTransformOperator(Substitute.For<ILogger<PixelToWorldTransformOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePixelToWorldTransform()
    {
        _operator.OperatorType.Should().Be(OperatorType.PixelToWorldTransform);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCalibrationData_ShouldReturnFailure()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCalibration_ShouldReturnSuccess()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = """{"CameraMatrix":[[500,0,160],[0,500,120],[0,0,1]],"DistCoeffs":[0,0,0,0,0],"ImageWidth":320,"ImageHeight":240}""";
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(100, 100) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithPixelToWorldMode_ShouldTransformPoints()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("TransformMode", "PixelToWorld"));
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = """{"CameraMatrix":[[500,0,160],[0,500,120],[0,0,1]],"DistCoeffs":[0,0,0,0,0]}""";
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(160, 120) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("TransformedPoints");
    }

    [Fact]
    public async Task ExecuteAsync_WithWorldToPixelMode_ShouldTransformPoints()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("TransformMode", "WorldToPixel"));
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = """{"CameraMatrix":[[500,0,160],[0,500,120],[0,0,1]],"DistCoeffs":[0,0,0,0,0]}""";
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(0, 0) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithValidUnitScale_ShouldBeValid()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("UnitScale", 0.001)); // mm to meters

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidUnitScale_ShouldBeInvalid()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("UnitScale", 0.0)); // 无效值

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
