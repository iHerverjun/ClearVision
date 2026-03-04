// ResultOutputOperatorTests.cs
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ResultOutputOperatorTests
{
    private readonly ResultOutputOperator _operator;

    public ResultOutputOperatorTests()
    {
        _operator = new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeResultOutput()
    {
        _operator.OperatorType.Should().Be(OperatorType.ResultOutput);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnSuccess()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithImageWrapper_ShouldKeepOutputImageUsable()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        var image = TestHelpers.CreateTestImage();

        try
        {
            var inputs = TestHelpers.CreateImageInputs(image);
            var result = await _operator.ExecuteAsync(op, inputs);

            result.IsSuccess.Should().BeTrue();
            result.OutputData.Should().NotBeNull();
            result.OutputData.Should().ContainKey("Image");

            var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
            var outputBytes = outputImage.GetBytes();
            outputBytes.Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithImageWrapperOnResultPort_ShouldKeepOutputUsable()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        var image = TestHelpers.CreateTestImage();

        try
        {
            var inputs = new Dictionary<string, object>
            {
                { "Result", image }
            };

            var result = await _operator.ExecuteAsync(op, inputs);

            result.IsSuccess.Should().BeTrue();
            result.OutputData.Should().NotBeNull();
            result.OutputData.Should().ContainKey("Result");

            var outputImage = result.OutputData!["Result"].Should().BeOfType<ImageWrapper>().Subject;
            var outputBytes = outputImage.GetBytes();
            outputBytes.Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }
        }
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithTextInput_ShouldPassThroughText()
    {
        var op = new Operator("test", OperatorType.ResultOutput, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            { "Text", "OCR result text" },
            { "IsSuccess", true }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Text");
        result.OutputData!["Text"].Should().Be("OCR result text");
        result.OutputData.Should().ContainKey("IsSuccess");
        result.OutputData["IsSuccess"].Should().Be(true);
    }
}
