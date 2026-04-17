using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class ImageNormalizeOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeImageNormalize()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ImageNormalize, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_MinMax_ShouldReturnImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "MinMax" },
            { "Alpha", 0.0 },
            { "Beta", 255.0 }
        });

        using var image = CreateGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Image"));
    }

    [Fact]
    public async Task ExecuteAsync_ColorInput_ShouldPreserveColorSemanticsByDefault()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "MinMax" },
            { "ColorMode", "LumaOnly" }
        });

        using var image = CreateColorGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        using var outputImage = Assert.IsType<ImageWrapper>(result.OutputData!["Image"]);
        var pixel = outputImage.GetMat().At<Vec3b>(20, 20);
        Assert.False(pixel.Item0 == pixel.Item1 && pixel.Item1 == pixel.Item2);
        Assert.Equal("LumaOnly", result.OutputData["ColorMode"]);
    }

    [Fact]
    public async Task ExecuteAsync_PerChannelMode_ShouldReturnThreeChannelImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ZScore" },
            { "ColorMode", "PerChannel" }
        });

        using var image = CreateColorGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        using var outputImage = Assert.IsType<ImageWrapper>(result.OutputData!["Image"]);
        Assert.Equal(3, outputImage.GetMat().Channels());
        Assert.Equal("PerChannel", result.OutputData["ColorMode"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSixteenBitColorInputInLumaOnlyMode_ShouldReturnUnifiedByteColorImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "MinMax" },
            { "ColorMode", "LumaOnly" }
        });

        using var image = CreateColorGradientImage16U();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        using var outputImage = Assert.IsType<ImageWrapper>(result.OutputData!["Image"]);
        var outputMat = outputImage.GetMat();
        Assert.Equal(MatType.CV_8UC3, outputMat.Type());
        Assert.Equal(3, outputMat.Channels());

        var pixel = outputMat.At<Vec3b>(30, 40);
        Assert.False(pixel.Item0 == pixel.Item1 && pixel.Item1 == pixel.Item2);
        Assert.Equal("LumaOnly", result.OutputData["ColorMode"]);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "CLAHE" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ImageNormalizeOperator CreateSut()
    {
        return new ImageNormalizeOperator(Substitute.For<ILogger<ImageNormalizeOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ImageNormalize", OperatorType.ImageNormalize, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateGradientImage()
    {
        var mat = new Mat(80, 100, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                var v = (byte)(x * 255 / mat.Cols);
                mat.Set(y, x, new Vec3b(v, v, v));
            }
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateColorGradientImage()
    {
        var mat = new Mat(60, 80, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                var blue = (byte)Math.Clamp(x * 255 / Math.Max(1, mat.Cols - 1), 0, 255);
                var green = (byte)Math.Clamp(y * 255 / Math.Max(1, mat.Rows - 1), 0, 255);
                var red = (byte)Math.Clamp(255 - ((x + y) * 255 / Math.Max(1, mat.Rows + mat.Cols - 2)), 0, 255);
                mat.Set(y, x, new Vec3b(blue, green, red));
            }
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateColorGradientImage16U()
    {
        var mat = new Mat(60, 80, MatType.CV_16UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                var blue = (ushort)Math.Clamp(x * ushort.MaxValue / Math.Max(1, mat.Cols - 1), 0, ushort.MaxValue);
                var green = (ushort)Math.Clamp(y * ushort.MaxValue / Math.Max(1, mat.Rows - 1), 0, ushort.MaxValue);
                var red = (ushort)Math.Clamp(ushort.MaxValue - ((x + y) * ushort.MaxValue / Math.Max(1, mat.Rows + mat.Cols - 2)), 0, ushort.MaxValue);
                mat.Set(y, x, new Vec3w(blue, green, red));
            }
        }

        return new ImageWrapper(mat);
    }
}

