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
        Assert.Equal(OperatorType.CornerDetection, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithCornerImage_ShouldReturnCorners()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "ShiTomasi" }, { "MaxCorners", 50 } });
        using var image = CreateTestImage();

        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) > 0);
    }

    [Fact]
    public async Task ExecuteAsync_WithRotatedNoisyRectangle_ShouldStillReturnCorners()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "ShiTomasi" }, { "MaxCorners", 50 } });
        using var image = CreateRotatedNoisyImage();

        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) > 0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        Assert.False(CreateSut().ValidateParameters(CreateOperator(new Dictionary<string, object> { { "Method", "FAST" } })).IsValid);
    }

    private static CornerDetectionOperator CreateSut() => new(Substitute.For<ILogger<CornerDetectionOperator>>());

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("CornerDetection", OperatorType.CornerDetection, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
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

    private static ImageWrapper CreateRotatedNoisyImage()
    {
        var mat = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        var pts = new[] { new Point(45, 30), new Point(120, 45), new Point(110, 120), new Point(35, 105) };
        Cv2.Polylines(mat, new[] { pts }, true, Scalar.White, 3);
        var rng = new Random(99);
        for (var i = 0; i < 80; i++)
        {
            mat.Set(rng.Next(mat.Rows), rng.Next(mat.Cols), new Vec3b(255, 255, 255));
        }

        return new ImageWrapper(mat);
    }
}
