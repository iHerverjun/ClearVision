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
public class CopyMakeBorderOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeCopyMakeBorder()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.CopyMakeBorder, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithPadding_ShouldExpandImageSize()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Top", 5 },
            { "Bottom", 6 },
            { "Left", 7 },
            { "Right", 8 },
            { "BorderType", "Constant" },
            { "Color", "#112233" }
        });

        using var image = CreateImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(55, Convert.ToInt32(result.OutputData!["Height"]));
        Assert.Equal(65, Convert.ToInt32(result.OutputData["Width"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidBorderType_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "BorderType", "Mirror101" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static CopyMakeBorderOperator CreateSut()
    {
        return new CopyMakeBorderOperator(Substitute.For<ILogger<CopyMakeBorderOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("CopyMakeBorder", OperatorType.CopyMakeBorder, 0, 0);
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
        var mat = new Mat(44, 50, MatType.CV_8UC3, Scalar.White);
        return new ImageWrapper(mat);
    }
}

