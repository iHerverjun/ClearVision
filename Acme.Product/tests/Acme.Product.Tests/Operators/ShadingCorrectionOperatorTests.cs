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
public class ShadingCorrectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeShadingCorrection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ShadingCorrection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithGaussianModel_ShouldReturnCorrectedImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "GaussianModel" },
            { "KernelSize", 31 }
        });

        using var image = CreateGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        using var outputImage = Assert.IsType<ImageWrapper>(result.OutputData!["Image"]);
        var outputMat = outputImage.GetMat();
        Assert.Equal(200, outputMat.Width);
        Assert.Equal(120, outputMat.Height);
    }

    [Fact]
    public async Task ExecuteAsync_DivideByBackgroundWithoutBackground_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "DivideByBackground" } });

        using var image = CreateGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "Homomorphic" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static ShadingCorrectionOperator CreateSut()
    {
        return new ShadingCorrectionOperator(Substitute.For<ILogger<ShadingCorrectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ShadingCorrection", OperatorType.ShadingCorrection, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateGradientImage()
    {
        var mat = new Mat(120, 200, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                var value = (byte)Math.Clamp((x * 255) / mat.Cols, 0, 255);
                mat.Set(y, x, new Vec3b(value, value, value));
            }
        }

        return new ImageWrapper(mat);
    }
}
