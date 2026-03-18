using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class StereoCalibrationOperatorTests
{
    private readonly StereoCalibrationOperator _operator;

    public StereoCalibrationOperatorTests()
    {
        _operator = new StereoCalibrationOperator(Substitute.For<ILogger<StereoCalibrationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeStereoCalibration()
    {
        _operator.OperatorType.Should().Be(OperatorType.StereoCalibration);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRightImage_ShouldReturnFailure()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        using var leftImage = TestHelpers.CreateTestImage();
        var inputs = new Dictionary<string, object> { { "LeftImage", leftImage } };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImages_ShouldReturnSuccess()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        using var leftImage = TestHelpers.CreateTestImage();
        using var rightImage = TestHelpers.CreateTestImage();
        var inputs = new Dictionary<string, object>
        {
            { "LeftImage", leftImage },
            { "RightImage", rightImage }
        };

        var result = await _operator.ExecuteAsync(op, inputs);
        // 没有标定板的图像会返回成功但可能不包含完整标定结果
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithValidParams_ShouldBeValid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 9));
        op.Parameters.Add(TestHelpers.CreateParameter("BoardHeight", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("SquareSize", 25.0));
        op.Parameters.Add(TestHelpers.CreateParameter("MinValidPairs", 12));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinValidPairs_ShouldBeInvalid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinValidPairs", 2)); // 小于最小值3

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidBoardDimensions_ShouldBeInvalid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 1)); // 无效值

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
