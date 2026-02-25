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
}

