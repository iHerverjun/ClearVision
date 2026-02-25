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
public class ImageStitchingOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeImageStitching()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ImageStitching, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ManualMode_ShouldReturnMergedImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "Manual" },
            { "OverlapPercent", 25.0 },
            { "BlendMode", "Linear" }
        });

        using var img1 = CreateImage1();
        using var img2 = CreateImage2();
        var inputs = new Dictionary<string, object>
        {
            { "Image1", img1 },
            { "Image2", img2 }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["OverlapRatio"]) > 0);
        Assert.True(Convert.ToInt32(result.OutputData["Width"]) > 120);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "HomographyOnly" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ImageStitchingOperator CreateSut()
    {
        return new ImageStitchingOperator(Substitute.For<ILogger<ImageStitchingOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ImageStitching", OperatorType.ImageStitching, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateImage1()
    {
        var mat = new Mat(80, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(20, 20, 60, 30), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateImage2()
    {
        var mat = new Mat(80, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(10, 20, 60, 30), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}

