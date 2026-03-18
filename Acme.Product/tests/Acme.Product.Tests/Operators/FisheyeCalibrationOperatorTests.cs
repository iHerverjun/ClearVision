using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class FisheyeCalibrationOperatorTests
{
    private readonly FisheyeCalibrationOperator _operator;

    public FisheyeCalibrationOperatorTests()
    {
        _operator = new FisheyeCalibrationOperator(Substitute.For<ILogger<FisheyeCalibrationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeFisheyeCalibration()
    {
        _operator.OperatorType.Should().Be(OperatorType.FisheyeCalibration);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        // 没有标定板的图像会返回成功但 Found=false
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithCalibrationParams_ShouldContainExpectedOutputs()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_WithValidBoardSize_ShouldBeValid()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 9));
        op.Parameters.Add(TestHelpers.CreateParameter("BoardHeight", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("SquareSize", 25.0));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidBoardWidth_ShouldBeInvalid()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 1)); // 小于最小值2

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidSquareSize_ShouldBeInvalid()
    {
        var op = new Operator("FisheyeCalibration", OperatorType.FisheyeCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("SquareSize", 0.0)); // 无效值

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
