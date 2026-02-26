using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class UndistortOperatorTests
{
    private readonly UndistortOperator _operator;

    public UndistortOperatorTests()
    {
        _operator = new UndistortOperator(Substitute.For<ILogger<UndistortOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeUndistort()
    {
        _operator.OperatorType.Should().Be(OperatorType.Undistort);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_With1dCameraMatrixCalibration_ShouldReturnSuccess()
    {
        var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] =
            "{\"CameraMatrix\":[500,0,160,0,500,120,0,0,1],\"DistCoeffs\":[0,0,0,0,0],\"ImageWidth\":320,\"ImageHeight\":240}";

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_With2dCameraMatrixCalibration_ShouldReturnSuccess()
    {
        var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] =
            "{\"CameraMatrix\":[[500,0,160],[0,500,120],[0,0,1]],\"DistCoeffs\":[0,0,0,0,0],\"ImageWidth\":320,\"ImageHeight\":240}";

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCameraMatrix_ShouldReturnFailure()
    {
        var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = "{\"DistCoeffs\":[0,0,0,0,0]}";

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("Undistort", OperatorType.Undistort, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
