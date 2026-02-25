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
public class ImageTilingOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeImageTiling()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ImageTiling, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_With2x2Grid_ShouldReturnFourTiles()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Rows", 2 },
            { "Cols", 2 },
            { "Overlap", 0 }
        });

        using var image = CreateImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(4, Convert.ToInt32(result.OutputData!["Count"]));
        var tiles = Assert.IsType<List<ImageWrapper>>(result.OutputData["Tiles"]);
        Assert.Equal(4, tiles.Count);
    }

    [Fact]
    public void ValidateParameters_WithInvalidRowsCols_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Rows", 0 }, { "Cols", 0 } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ImageTilingOperator CreateSut()
    {
        return new ImageTilingOperator(Substitute.For<ILogger<ImageTilingOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ImageTiling", OperatorType.ImageTiling, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateImage()
    {
        var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.White);
        return new ImageWrapper(mat);
    }
}

