using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ImageBlendOperatorTests
{
    private readonly ImageBlendOperator _operator =
        new(Substitute.For<ILogger<ImageBlendOperator>>());

    [Fact]
    public void OperatorType_ShouldBeImageBlend()
    {
        _operator.OperatorType.Should().Be(OperatorType.ImageBlend);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameSizeImages_ShouldBlendPixels()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Alpha", 0.5, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Beta", 0.5, "double"));

        using var background = CreateSolidColorImage(2, 2, 50);
        using var foreground = CreateSolidColorImage(2, 2, 150);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Background"] = background,
            ["Foreground"] = foreground
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        var pixel = outputImage.MatReadOnly.At<Vec3b>(0, 0);
        pixel.Item0.Should().Be(100);
        pixel.Item1.Should().Be(100);
        pixel.Item2.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentSizedImages_ShouldResizeForegroundAndBlendResizedPixels()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Alpha", 0.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Beta", 1.0, "double"));

        using var background = CreateSolidColorImage(4, 4, 10);
        using var foreground = CreateBlendPatternImage();
        using var expected = new Mat();
        Cv2.Resize(foreground.MatReadOnly, expected, new Size(4, 4));
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Background"] = background,
            ["Foreground"] = foreground
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Width.Should().Be(4);
        outputImage.Height.Should().Be(4);

        for (var y = 0; y < expected.Rows; y++)
        {
            for (var x = 0; x < expected.Cols; x++)
            {
                AssertPixel(outputImage.MatReadOnly, expected.At<Vec3b>(y, x), x, y);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithoutForeground_ShouldReturnFailure()
    {
        using var background = CreateSolidColorImage(2, 2, 50);
        var result = await _operator.ExecuteAsync(CreateOperator(), new Dictionary<string, object>
        {
            ["Background"] = background
        });

        result.IsSuccess.Should().BeFalse();
    }

    private static Operator CreateOperator()
    {
        return new Operator("ImageBlend", OperatorType.ImageBlend, 0, 0);
    }

    private static ImageWrapper CreateSolidColorImage(int width, int height, byte value)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(value, value, value));
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateBlendPatternImage()
    {
        var mat = new Mat(2, 2, MatType.CV_8UC3);
        mat.Set(0, 0, new Vec3b(20, 40, 60));
        mat.Set(0, 1, new Vec3b(80, 120, 160));
        mat.Set(1, 0, new Vec3b(180, 60, 30));
        mat.Set(1, 1, new Vec3b(240, 220, 200));
        return new ImageWrapper(mat);
    }

    private static void AssertPixel(Mat actual, Vec3b expected, int x, int y)
    {
        var pixel = actual.At<Vec3b>(y, x);
        pixel.Item0.Should().Be(expected.Item0);
        pixel.Item1.Should().Be(expected.Item1);
        pixel.Item2.Should().Be(expected.Item2);
    }
}
