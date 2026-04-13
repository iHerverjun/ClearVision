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
public class QuadrilateralFindOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeQuadrilateralFind()
    {
        Assert.Equal(OperatorType.QuadrilateralFind, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnStableOrderedVertices()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 100 },
            { "MaxArea", 100000 },
            { "ApproxEpsilon", 0.02 },
            { "ConvexOnly", true }
        });

        using var image = CreateQuadImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
        var ordered = Assert.IsType<List<Position>>(result.OutputData["OrderedVertices"]);
        Assert.Equal(4, ordered.Count);
        var first = ordered[0];
        Assert.True(ordered.All(point => first.Y <= point.Y + 5 || first.X <= point.X + 5));
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleNoisyQuadrilaterals_ShouldFindMoreThanOne()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 100 },
            { "MaxArea", 100000 },
            { "ApproxEpsilon", 0.02 },
            { "ConvexOnly", false }
        });

        using var image = CreateMultipleQuadImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 2);
    }

    [Fact]
    public void ValidateParameters_WithInvalidAreaRange_ShouldReturnInvalid()
    {
        Assert.False(CreateSut().ValidateParameters(CreateOperator(new Dictionary<string, object> { { "MinArea", 1000 }, { "MaxArea", 100 } })).IsValid);
    }

    private static QuadrilateralFindOperator CreateSut() => new(Substitute.For<ILogger<QuadrilateralFindOperator>>());

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("QuadrilateralFind", OperatorType.QuadrilateralFind, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateQuadImage()
    {
        var mat = new Mat(180, 220, MatType.CV_8UC3, Scalar.Black);
        var pts = new[] { new Point(50, 40), new Point(170, 55), new Point(150, 140), new Point(60, 130) };
        Cv2.Polylines(mat, new[] { pts }, true, Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateMultipleQuadImage()
    {
        var mat = new Mat(220, 260, MatType.CV_8UC3, Scalar.Black);
        var pts1 = new[] { new Point(25, 35), new Point(110, 45), new Point(95, 120), new Point(30, 110) };
        var pts2 = new[] { new Point(150, 80), new Point(220, 60), new Point(235, 145), new Point(165, 160) };
        Cv2.Polylines(mat, new[] { pts1 }, true, Scalar.White, 3);
        Cv2.Polylines(mat, new[] { pts2 }, true, Scalar.White, 3);
        var rng = new Random(7);
        for (var i = 0; i < 120; i++)
        {
            mat.Set(rng.Next(mat.Rows), rng.Next(mat.Cols), new Vec3b(255, 255, 255));
        }

        return new ImageWrapper(mat);
    }
}
