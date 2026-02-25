using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class PolarUnwrapOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePolarUnwrap()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.PolarUnwrap, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRingImage_ShouldReturnUnwrappedImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CenterX", 60 },
            { "CenterY", 60 },
            { "InnerRadius", 20 },
            { "OuterRadius", 50 },
            { "OutputWidth", 180 }
        });

        using var image = CreateRingImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(180, Convert.ToInt32(result.OutputData!["Width"]));
        Assert.Equal(30, Convert.ToInt32(result.OutputData["Height"]));

        using var outputImage = Assert.IsType<ImageWrapper>(result.OutputData["Image"]);
        var outputMat = outputImage.GetMat();
        Assert.Equal(180, outputMat.Width);
        Assert.Equal(30, outputMat.Height);
    }

    [Fact]
    public void ValidateParameters_WithInvalidRadii_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "InnerRadius", 50 },
            { "OuterRadius", 20 }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static PolarUnwrapOperator CreateSut()
    {
        return new PolarUnwrapOperator(Substitute.For<ILogger<PolarUnwrapOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PolarUnwrap", OperatorType.PolarUnwrap, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateRingImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(60, 60), 40, Scalar.White, 2);
        Cv2.Circle(mat, new Point(60, 60), 25, new Scalar(127, 127, 127), 2);
        return new ImageWrapper(mat);
    }
}
