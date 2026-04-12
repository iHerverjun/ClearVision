using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.TestData;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class FisheyeUndistortOperatorTests
{
    private readonly FisheyeUndistortOperator _operator;

    public FisheyeUndistortOperatorTests()
    {
        _operator = new FisheyeUndistortOperator(Substitute.For<ILogger<FisheyeUndistortOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeFisheyeUndistort()
    {
        _operator.OperatorType.Should().Be(OperatorType.FisheyeUndistort);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCalibrationData_ShouldReturnFailure()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidFisheyeCalibration_ShouldReturnSuccess()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson(fisheye: true);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithStandardCalibration_ShouldReturnFailure()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson();

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithValidBalance_ShouldBeValid()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Balance", 0.5));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidBalance_ShouldBeInvalid()
    {
        var op = new Operator("FisheyeUndistort", OperatorType.FisheyeUndistort, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Balance", 1.5)); // 超出范围

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
