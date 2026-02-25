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
public class RectangleDetectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeRectangleDetection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.RectangleDetection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithRectangleImage_ShouldDetectAtLeastOneRectangle()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 500 },
            { "MaxArea", 200000 },
            { "AngleTolerance", 20.0 },
            { "ApproxEpsilon", 0.02 }
        });

        using var image = CreateRectangleImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
        Assert.True(Convert.ToDouble(result.OutputData["Width"]) > 0);
        Assert.True(Convert.ToDouble(result.OutputData["Height"]) > 0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidAreaRange_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 1000 },
            { "MaxArea", 100 }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static RectangleDetectionOperator CreateSut()
    {
        return new RectangleDetectionOperator(Substitute.For<ILogger<RectangleDetectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("RectangleDetection", OperatorType.RectangleDetection, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateRectangleImage()
    {
        var mat = new Mat(200, 220, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 60, 100, 70), Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}
