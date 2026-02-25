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
public class BlobLabelingOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeBlobLabeling()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.BlobLabeling, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithTwoBlobs_ShouldReturnLabels()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "LabelBy", "Area" },
            { "DrawLabels", true }
        });

        using var image = CreateBlobImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
        Assert.True(result.OutputData.ContainsKey("Labels"));
    }

    [Fact]
    public void ValidateParameters_WithInvalidLabelBy_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "LabelBy", "Color" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static BlobLabelingOperator CreateSut()
    {
        return new BlobLabelingOperator(Substitute.For<ILogger<BlobLabelingOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("BlobLabeling", OperatorType.BlobLabeling, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateBlobImage()
    {
        var mat = new Mat(120, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(40, 60), 15, Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(90, 40, 30, 40), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}

