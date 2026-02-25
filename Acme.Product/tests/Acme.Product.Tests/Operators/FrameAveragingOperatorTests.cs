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
public class FrameAveragingOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeFrameAveraging()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.FrameAveraging, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_MeanModeAcrossTwoFrames_ShouldIncreaseFrameCount()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FrameCount", 3 },
            { "Mode", "Mean" }
        });

        using var frame1 = CreateSolidImage(20);
        using var frame2 = CreateSolidImage(60);

        var first = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(frame1));
        var second = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(frame2));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, Convert.ToInt32(first.OutputData!["FrameCount"]));
        Assert.Equal(2, Convert.ToInt32(second.OutputData!["FrameCount"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "WeightedMean" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static FrameAveragingOperator CreateSut()
    {
        return new FrameAveragingOperator(Substitute.For<ILogger<FrameAveragingOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("FrameAveraging", OperatorType.FrameAveraging, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateSolidImage(byte value)
    {
        var mat = new Mat(80, 80, MatType.CV_8UC3, new Scalar(value, value, value));
        return new ImageWrapper(mat);
    }
}
