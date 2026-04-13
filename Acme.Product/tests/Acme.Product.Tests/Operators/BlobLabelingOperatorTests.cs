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
        Assert.Equal(OperatorType.BlobLabeling, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithImageBlobs_ShouldReturnLabels()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "LabelBy", "Area" }, { "DrawLabels", true } });
        using var image = CreateBlobImage();

        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.True(Convert.ToInt32(result.OutputData!["Count"]) >= 1);
        Assert.True(result.OutputData.ContainsKey("Labels"));
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedBlobs_ShouldPreferInputBlobs()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "LabelBy", "Position" } });
        using var image = new ImageWrapper(new Mat(120, 160, MatType.CV_8UC3, Scalar.Black));
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Blobs"] = new List<Dictionary<string, object>>
        {
            new() { { "X", 10 }, { "Y", 10 }, { "Width", 20 }, { "Height", 20 } },
            new() { { "X", 80 }, { "Y", 70 }, { "Width", 30 }, { "Height", 25 } }
        };

        var result = await CreateSut().ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Convert.ToInt32(result.OutputData!["Count"]));
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidThresholds_ShouldFail()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Thresholds", "[{\"Name\":\"A\",\"Min\":10,\"Max\":1}]" } });
        using var image = CreateBlobImage();

        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateParameters_WithInvalidLabelBy_ShouldReturnInvalid()
    {
        Assert.False(CreateSut().ValidateParameters(CreateOperator(new Dictionary<string, object> { { "LabelBy", "Color" } })).IsValid);
    }

    private static BlobLabelingOperator CreateSut() => new(Substitute.For<ILogger<BlobLabelingOperator>>());

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("BlobLabeling", OperatorType.BlobLabeling, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
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
