using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ThresholdingOperatorTests
{
    private readonly ThresholdOperator _operator =
        new(Substitute.For<ILogger<ThresholdOperator>>());

    [Fact]
    public void OperatorType_ShouldMapToThresholding()
    {
        _operator.OperatorType.Should().Be(OperatorType.Thresholding);
    }

    [Fact]
    public async Task ExecuteAsync_WithBinaryThreshold_ShouldReturnSingleChannelMask()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 100.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MaxValue", 255.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Type", (int)ThresholdTypes.Binary, "int"));

        using var image = CreateTwoToneImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Channels.Should().Be(1);
        outputImage.MatReadOnly.At<byte>(0, 0).Should().Be(0);
        outputImage.MatReadOnly.At<byte>(0, 1).Should().Be(255);
        result.OutputData["ActualThreshold"].Should().Be(100.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithOtsu_ShouldExposeOtsuThreshold()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("UseOtsu", true, "bool"));

        using var image = CreateTwoToneImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("OtsuThreshold");
        result.OutputData.Should().ContainKey("ActualThreshold");
    }

    [Fact]
    public async Task ExecuteAsync_WithTriangleAndUseOtsu_ShouldReturnFailure()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Type", (int)ThresholdTypes.Triangle, "int"));
        op.AddParameter(TestHelpers.CreateParameter("UseOtsu", true, "bool"));

        using var image = CreateTwoToneImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("UseOtsu");
    }

    private static Operator CreateOperator()
    {
        return new Operator("Thresholding", OperatorType.Thresholding, 0, 0);
    }

    private static ImageWrapper CreateTwoToneImage()
    {
        var mat = new Mat(1, 2, MatType.CV_8UC1);
        mat.Set(0, 0, (byte)50);
        mat.Set(0, 1, (byte)200);
        return new ImageWrapper(mat);
    }
}
