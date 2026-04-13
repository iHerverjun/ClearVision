using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ImageDiffOperatorTests
{
    private readonly ImageDiffOperator _operator =
        new(Substitute.For<ILogger<ImageDiffOperator>>());

    [Fact]
    public void OperatorType_ShouldBeImageDiff()
    {
        _operator.OperatorType.Should().Be(OperatorType.ImageDiff);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentImages_ShouldReturnFullDiffRate()
    {
        using var baseImage = CreateGrayImage(3, 3, 0);
        using var compareImage = CreateGrayImage(3, 3, 255);
        var result = await _operator.ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            ["BaseImage"] = baseImage,
            ["CompareImage"] = compareImage
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("DiffRate");
        Convert.ToDouble(result.OutputData!["DiffRate"]).Should().Be(1.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithIdenticalImages_ShouldReturnZeroDiffRate()
    {
        using var baseImage = CreateGrayImage(3, 3, 128);
        using var compareImage = CreateGrayImage(3, 3, 128);
        var result = await _operator.ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            ["BaseImage"] = baseImage,
            ["CompareImage"] = compareImage
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["DiffRate"]).Should().Be(0.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMismatchedSizes_ShouldReturnFailure()
    {
        using var baseImage = CreateGrayImage(3, 3, 0);
        using var compareImage = CreateGrayImage(4, 4, 255);
        var result = await _operator.ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            ["BaseImage"] = baseImage,
            ["CompareImage"] = compareImage
        });

        result.IsSuccess.Should().BeFalse();
    }

    private static Operator CreateOperator()
    {
        return new Operator("ImageDiff", OperatorType.ImageDiff, 0, 0);
    }

    private static ImageWrapper CreateGrayImage(int width, int height, byte value)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(value));
        return new ImageWrapper(mat);
    }
}
