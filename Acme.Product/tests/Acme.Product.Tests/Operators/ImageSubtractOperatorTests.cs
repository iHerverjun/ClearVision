using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ImageSubtractOperatorTests
{
    private readonly ImageSubtractOperator _operator;

    public ImageSubtractOperatorTests()
    {
        _operator = new ImageSubtractOperator(Substitute.For<ILogger<ImageSubtractOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeImageSubtract()
    {
        _operator.OperatorType.Should().Be(OperatorType.ImageSubtract);
    }

    [Fact]
    public async Task ExecuteAsync_WithColorImages_ShouldReturnStatistics()
    {
        var op = new Operator("Subtract", OperatorType.ImageSubtract, 0, 0);
        using var image1 = CreateImage(new Scalar(100, 30, 10), 200, 120);
        using var image2 = CreateImage(new Scalar(80, 50, 5), 200, 120);
        var inputs = new Dictionary<string, object>
        {
            { "Image1", image1 },
            { "Image2", image2 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("MinDifference");
        result.OutputData.Should().ContainKey("MaxDifference");
        result.OutputData.Should().ContainKey("MeanDifference");
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentSizes_ShouldResizeAndSucceed()
    {
        var op = new Operator("Subtract", OperatorType.ImageSubtract, 0, 0);
        using var image1 = CreateImage(new Scalar(100, 30, 10), 220, 160);
        using var image2 = CreateImage(new Scalar(80, 50, 5), 100, 80);
        var inputs = new Dictionary<string, object>
        {
            { "Image1", image1 },
            { "Image2", image2 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    private static ImageWrapper CreateImage(Scalar color, int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, color);
        return new ImageWrapper(mat);
    }
}
