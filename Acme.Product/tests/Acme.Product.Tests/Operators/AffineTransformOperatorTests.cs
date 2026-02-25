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
public class AffineTransformOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeAffineTransform()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.AffineTransform, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_RotateScaleTranslateMode_ShouldReturnMatrixAndImage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "RotateScaleTranslate" },
            { "Angle", 0.0 },
            { "Scale", 1.0 },
            { "TranslateX", 10.0 },
            { "TranslateY", 5.0 },
            { "OutputWidth", 160 },
            { "OutputHeight", 120 }
        });

        using var image = CreateTestImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);

        var matrix = Assert.IsType<double[][]>(result.OutputData!["TransformMatrix"]);
        Assert.Equal(10.0, matrix[0][2], 1);
        Assert.Equal(5.0, matrix[1][2], 1);
        Assert.Equal(160, Convert.ToInt32(result.OutputData["Width"]));
        Assert.Equal(120, Convert.ToInt32(result.OutputData["Height"]));
    }

    [Fact]
    public async Task ExecuteAsync_ThreePointModeWithInvalidPoints_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "ThreePoint" },
            { "SrcPoints", "[]" },
            { "DstPoints", "[[0,0],[10,0],[0,10]]" }
        });

        using var image = CreateTestImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "Projective" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static AffineTransformOperator CreateSut()
    {
        return new AffineTransformOperator(Substitute.For<ILogger<AffineTransformOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("AffineTransform", OperatorType.AffineTransform, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateTestImage()
    {
        var mat = new Mat(120, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(40, 30, 40, 30), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
