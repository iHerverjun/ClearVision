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
        var sut = CreateSut();
        Assert.Equal(OperatorType.QuadrilateralFind, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithQuadShape_ShouldFindAtLeastOne()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 100 },
            { "MaxArea", 100000 },
            { "ApproxEpsilon", 0.02 },
            { "ConvexOnly", false }
        });

        using var image = CreateQuadImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
    }

    [Fact]
    public void ValidateParameters_WithInvalidAreaRange_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "MinArea", 1000 }, { "MaxArea", 100 } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static QuadrilateralFindOperator CreateSut()
    {
        return new QuadrilateralFindOperator(Substitute.For<ILogger<QuadrilateralFindOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("QuadrilateralFind", OperatorType.QuadrilateralFind, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateQuadImage()
    {
        var mat = new Mat(180, 220, MatType.CV_8UC3, Scalar.Black);
        var pts = new[]
        {
            new Point(50, 40),
            new Point(170, 55),
            new Point(150, 140),
            new Point(60, 130)
        };
        Cv2.Polylines(mat, new[] { pts }, true, Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}

