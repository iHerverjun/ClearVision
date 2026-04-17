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
        Assert.Equal(OperatorType.RectangleDetection, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithRectangleImage_ShouldExposeNormalizedGeometry()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 500 },
            { "MaxArea", 200000 },
            { "AngleTolerance", 20.0 },
            { "ApproxEpsilon", 0.02 }
        });

        using var image = CreateRectangleImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
        Assert.True(Convert.ToDouble(result.OutputData["LongSide"]) >= Convert.ToDouble(result.OutputData["ShortSide"]));
        Assert.InRange(Convert.ToDouble(result.OutputData["NormalizedAngle"]), -90.0, 90.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleNoisyRectangles_ShouldDetectMoreThanOne()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 300 },
            { "MaxArea", 200000 },
            { "AngleTolerance", 25.0 },
            { "ApproxEpsilon", 0.02 }
        });

        using var image = CreateMultipleRectangleImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExposeRefinedFloatingPointVertices()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 200 },
            { "MaxArea", 200000 },
            { "AngleTolerance", 25.0 },
            { "ApproxEpsilon", 0.02 }
        });

        using var image = CreateBlurredRectangleImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        var rectangles = Assert.IsType<List<Dictionary<string, object>>>(result.OutputData!["Rectangles"]);
        var points = Assert.IsType<List<Dictionary<string, object>>>(rectangles[0]["Points"]);
        Assert.Equal(4, points.Count);
        Assert.All(points, point =>
        {
            Assert.InRange(Convert.ToDouble(point["X"]), 0.0, 240.0);
            Assert.InRange(Convert.ToDouble(point["Y"]), 0.0, 220.0);
        });
    }

    [Fact]
    public void ValidateParameters_WithInvalidAreaRange_ShouldReturnInvalid()
    {
        var validation = CreateSut().ValidateParameters(CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 1000 },
            { "MaxArea", 100 }
        }));

        Assert.False(validation.IsValid);
    }

    private static RectangleDetectionOperator CreateSut() => new(Substitute.For<ILogger<RectangleDetectionOperator>>());

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
        var mat = new Mat(220, 240, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 60, 100, 70), Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateMultipleRectangleImage()
    {
        var mat = new Mat(240, 260, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(20, 30, 90, 60), Scalar.White, 3);
        Cv2.Rectangle(mat, new Rect(140, 110, 80, 50), Scalar.White, 3);
        var rng = new Random(42);
        for (var i = 0; i < 120; i++)
        {
            mat.Set(rng.Next(mat.Rows), rng.Next(mat.Cols), new Vec3b(255, 255, 255));
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateBlurredRectangleImage()
    {
        var mat = new Mat(220, 240, MatType.CV_8UC3, Scalar.Black);
        using var baseShape = new Mat(220, 240, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(baseShape, new Rect(60, 70, 100, 60), Scalar.White, -1);
        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(120, 110), 11.7, 1.0);
        Cv2.WarpAffine(baseShape, mat, rotation, baseShape.Size(), InterpolationFlags.Linear, BorderTypes.Constant);
        using var blurred = new Mat();
        Cv2.GaussianBlur(mat, blurred, new Size(9, 9), 1.8);
        return new ImageWrapper(blurred.Clone());
    }
}
