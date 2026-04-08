using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class AdaptiveThresholdOperatorTests
{
    private readonly AdaptiveThresholdOperator _operator;

    public AdaptiveThresholdOperatorTests()
    {
        _operator = new AdaptiveThresholdOperator(Substitute.For<ILogger<AdaptiveThresholdOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeAdaptiveThreshold()
    {
        _operator.OperatorType.Should().Be(OperatorType.AdaptiveThreshold);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.AdaptiveThreshold, 0, 0);

        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("test", OperatorType.AdaptiveThreshold, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.AdaptiveThreshold, 0, 0);

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Binary", (byte)255)]
    [InlineData("BinaryInv", (byte)0)]
    public async Task ExecuteAsync_WithUniformImage_ShouldRespectThresholdPolarity_AndKeepSingleChannelOutput(
        string thresholdType,
        byte expectedValue)
    {
        var op = new Operator("adaptive", OperatorType.AdaptiveThreshold, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ThresholdType", thresholdType, "string"));
        op.AddParameter(TestHelpers.CreateParameter("AdaptiveMethod", "Mean", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BlockSize", 3, "int"));
        op.AddParameter(TestHelpers.CreateParameter("C", 2.0, "double"));

        using var image = CreateUniformGrayImage(128);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Channels.Should().Be(1);

        var output = outputImage.MatReadOnly;
        Cv2.CountNonZero(output).Should().Be(expectedValue == 0 ? 0 : output.Rows * output.Cols);
        output.At<byte>(0, 0).Should().Be(expectedValue);
    }

    private static ImageWrapper CreateUniformGrayImage(byte value)
    {
        var mat = new Mat(9, 9, MatType.CV_8UC1, new Scalar(value));
        return new ImageWrapper(mat);
    }
}
