using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MedianBlurOperatorTests
{
    private readonly MedianBlurOperator _operator;

    public MedianBlurOperatorTests()
    {
        _operator = new MedianBlurOperator(Substitute.For<ILogger<MedianBlurOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeMedianBlur()
    {
        _operator.OperatorType.Should().Be(OperatorType.MedianBlur);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.MedianBlur, 0, 0);

        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("test", OperatorType.MedianBlur, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.MedianBlur, 0, 0);

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSaltAndPepperNoise_ShouldSuppressImpulseNoise()
    {
        var op = new Operator("median", OperatorType.MedianBlur, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 3, "int"));

        using var image = CreateSaltAndPepperImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;

        var output = outputImage.MatReadOnly;
        output.At<byte>(2, 2).Should().Be(0);
    }

    private static ImageWrapper CreateSaltAndPepperImage()
    {
        var mat = new Mat(5, 5, MatType.CV_8UC1, Scalar.Black);
        mat.Set(2, 2, byte.MaxValue);
        return new ImageWrapper(mat);
    }
}
