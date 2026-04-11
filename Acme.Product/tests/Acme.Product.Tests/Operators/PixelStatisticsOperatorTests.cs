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
public class PixelStatisticsOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePixelStatistics()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.PixelStatistics, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ConstantImage_ShouldReturnExpectedMean()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Channel", "Gray" }
        });

        using var image = CreateConstantImage(100);
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(100.0, Convert.ToDouble(result.OutputData!["Mean"]), 3);
        Assert.Equal(100.0, Convert.ToDouble(result.OutputData["Min"]), 3);
        Assert.Equal(100.0, Convert.ToDouble(result.OutputData["Max"]), 3);
        Assert.True(result.OutputData.ContainsKey("StatusCode"));
    }

    [Fact]
    public void ValidateParameters_WithInvalidChannel_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Channel", "LAB" } });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static PixelStatisticsOperator CreateSut()
    {
        return new PixelStatisticsOperator(Substitute.For<ILogger<PixelStatisticsOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PixelStatistics", OperatorType.PixelStatistics, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateConstantImage(byte value)
    {
        var mat = new Mat(60, 80, MatType.CV_8UC3, new Scalar(value, value, value));
        return new ImageWrapper(mat);
    }
}
