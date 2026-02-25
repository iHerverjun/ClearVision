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
public class EdgePairDefectOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeEdgePairDefect()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.EdgePairDefect, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedParallelLines_ShouldHaveZeroDefects()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ExpectedWidth", 20.0 },
            { "Tolerance", 1.0 },
            { "NumSamples", 25 },
            { "EdgeMethod", "Canny" }
        });

        using var image = CreateBlankImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(10, 20, 110, 20);
        inputs["Line2"] = new LineData(10, 40, 110, 40);

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(0, Convert.ToInt32(result.OutputData!["DefectCount"]));
        Assert.Equal(0.0, Convert.ToDouble(result.OutputData["MaxDeviation"]), 3);
    }

    [Fact]
    public void ValidateParameters_WithInvalidEdgeMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "EdgeMethod", "Laplacian" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static EdgePairDefectOperator CreateSut()
    {
        return new EdgePairDefectOperator(Substitute.For<ILogger<EdgePairDefectOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("EdgePairDefect", OperatorType.EdgePairDefect, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateBlankImage()
    {
        var mat = new Mat(120, 140, MatType.CV_8UC3, Scalar.Black);
        return new ImageWrapper(mat);
    }
}
