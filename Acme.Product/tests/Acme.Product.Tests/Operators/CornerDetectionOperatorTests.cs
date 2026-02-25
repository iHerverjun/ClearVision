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
public class CornerDetectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeCornerDetection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.CornerDetection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithCornerImage_ShouldReturnCorners()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ShiTomasi" },
            { "MaxCorners", 50 }
        });

        using var image = CreateTestImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) > 0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "FAST" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static CornerDetectionOperator CreateSut()
    {
        return new CornerDetectionOperator(Substitute.For<ILogger<CornerDetectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("CornerDetection", OperatorType.CornerDetection, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateTestImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(20, 20, 80, 80), Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}

