// TemplateMatchOperatorTests.cs
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class TemplateMatchOperatorTests
{
    private readonly TemplateMatchOperator _operator;

    public TemplateMatchOperatorTests()
    {
        _operator = new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTemplateMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.TemplateMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplate_ShouldKeepOutputImageUsable()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);

        using var src = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(src, new Rect(30, 30, 40, 40), Scalar.White, -1);

        using var templateRoi = new Mat(src, new Rect(30, 30, 40, 40));
        using var template = templateRoi.Clone();

        var inputs = new Dictionary<string, object>
        {
            { "Image", src.ToBytes(".png") },
            { "Template", template.ToBytes(".png") }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Image");

        var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        var outputBytes = outputImage.GetBytes();
        outputBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
