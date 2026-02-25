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
public class SurfaceDefectDetectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeSurfaceDefectDetection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.SurfaceDefectDetection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ReferenceDiffMode_ShouldDetectDefect()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ReferenceDiff" },
            { "Threshold", 10.0 },
            { "MinArea", 20 },
            { "MaxArea", 100000 },
            { "MorphCleanSize", 3 }
        });

        using var source = CreateSourceWithDefect();
        using var reference = CreateReferenceImage();
        var inputs = TestHelpers.CreateImageInputs(source);
        inputs["Reference"] = reference;

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["DefectCount"]) >= 1);
        Assert.True(result.OutputData.ContainsKey("DefectMask"));
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "PhaseOnly" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static SurfaceDefectDetectionOperator CreateSut()
    {
        return new SurfaceDefectDetectionOperator(Substitute.For<ILogger<SurfaceDefectDetectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("SurfaceDefectDetection", OperatorType.SurfaceDefectDetection, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateSourceWithDefect()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(40, 40, 30, 30), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateReferenceImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        return new ImageWrapper(mat);
    }
}
