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
public class ImageComposeOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeImageCompose()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ImageCompose, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_Horizontal_ShouldIncreaseWidth()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Horizontal" },
            { "Padding", 5 },
            { "BackgroundColor", "#000000" }
        });

        using var image1 = CreateImage(new Scalar(255, 0, 0));
        using var image2 = CreateImage(new Scalar(0, 255, 0));
        var inputs = new Dictionary<string, object> { { "Image1", image1 }, { "Image2", image2 } };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(105, Convert.ToInt32(result.OutputData!["Width"]));
        Assert.Equal(50, Convert.ToInt32(result.OutputData["Height"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "Overlay" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ImageComposeOperator CreateSut()
    {
        return new ImageComposeOperator(Substitute.For<ILogger<ImageComposeOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ImageCompose", OperatorType.ImageCompose, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateImage(Scalar color)
    {
        var mat = new Mat(50, 50, MatType.CV_8UC3, color);
        return new ImageWrapper(mat);
    }
}

