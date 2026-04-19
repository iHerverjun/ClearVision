using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class PixelStatisticsOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePixelStatistics()
    {
        CreateSut().OperatorType.Should().Be(OperatorType.PixelStatistics);
    }

    [Fact]
    public async Task ExecuteAsync_ConstantImage_ShouldReturnExpectedMean()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "Gray" });

        using var image = CreateConstantImage(100);
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Mean"]).Should().BeApproximately(100.0, 1e-3);
        Convert.ToDouble(result.OutputData["Min"]).Should().BeApproximately(100.0, 1e-3);
        Convert.ToDouble(result.OutputData["Max"]).Should().BeApproximately(100.0, 1e-3);
        Convert.ToDouble(result.OutputData["StdError"]).Should().BeApproximately(0.0, 1e-9);
        Convert.ToDouble(result.OutputData["MedianAbsoluteDeviation"]).Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public async Task ExecuteAsync_ChannelAll_ShouldFlattenChannelsAndExposePerChannelStats()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "All" });

        using var mat = new Mat(1, 2, MatType.CV_8UC3);
        mat.Set(0, 0, new Vec3b(10, 20, 30));
        mat.Set(0, 1, new Vec3b(40, 50, 60));

        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(new ImageWrapper(mat)));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Mean"]).Should().BeApproximately(35.0, 1e-3);
        result.OutputData["AggregationMode"].Should().Be("FlattenedChannels");

        var channelStats = result.OutputData["ChannelStats"]
            .Should()
            .BeOfType<Dictionary<string, object>>()
            .Subject;

        Convert.ToDouble(((Dictionary<string, object>)channelStats["B"])["Mean"]).Should().BeApproximately(25.0, 1e-3);
        Convert.ToDouble(((Dictionary<string, object>)channelStats["G"])["Mean"]).Should().BeApproximately(35.0, 1e-3);
        Convert.ToDouble(((Dictionary<string, object>)channelStats["R"])["Mean"]).Should().BeApproximately(45.0, 1e-3);
    }

    [Fact]
    public async Task ExecuteAsync_FloatImage_ShouldComputeExactMedian()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "Gray" });

        using var mat = new Mat(1, 4, MatType.CV_32FC1);
        mat.Set(0, 0, 0.1f);
        mat.Set(0, 1, 0.2f);
        mat.Set(0, 2, 1.7f);
        mat.Set(0, 3, 5.0f);

        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(new ImageWrapper(mat)));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Median"]).Should().BeApproximately(0.95, 1e-6);
        Convert.ToDouble(result.OutputData["Max"]).Should().BeApproximately(5.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_WithMask_ShouldReturnAnalyticStatsAndUncertainty()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "Gray" });

        using var mat = new Mat(2, 2, MatType.CV_32FC1);
        mat.Set(0, 0, 2.0f);
        mat.Set(0, 1, 4.0f);
        mat.Set(1, 0, 6.0f);
        mat.Set(1, 1, 8.0f);

        using var maskMat = new Mat(2, 2, MatType.CV_8UC1, Scalar.Black);
        maskMat.Set(0, 0, (byte)255);
        maskMat.Set(1, 0, (byte)255);
        maskMat.Set(1, 1, (byte)255);

        var inputs = TestHelpers.CreateImageInputs(new ImageWrapper(mat));
        inputs["Mask"] = new ImageWrapper(maskMat);

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Mean"]).Should().BeApproximately(16.0 / 3.0, 1e-6);
        Convert.ToDouble(result.OutputData["Median"]).Should().BeApproximately(6.0, 1e-6);
        Convert.ToDouble(result.OutputData["Range"]).Should().BeApproximately(6.0, 1e-6);
        Convert.ToDouble(result.OutputData["MedianAbsoluteDeviation"]).Should().BeApproximately(2.0, 1e-6);
        Convert.ToDouble(result.OutputData["StdDev"]).Should().BeApproximately(Math.Sqrt(56.0 / 9.0), 1e-6);
        Convert.ToDouble(result.OutputData["StdError"]).Should().BeApproximately(Math.Sqrt(56.0 / 27.0), 1e-6);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeApproximately(Math.Sqrt(56.0 / 27.0), 1e-6);
        Convert.ToInt32(result.OutputData["NonZeroCount"]).Should().Be(3);
        Convert.ToInt32(result.OutputData["SampleCount"]).Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RoiAndMismatchedMask_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Channel"] = "Gray",
            ["RoiX"] = 10,
            ["RoiY"] = 10,
            ["RoiW"] = 20,
            ["RoiH"] = 20
        });

        using var image = CreateConstantImage(100);
        using var mask = new ImageWrapper(new Mat(8, 8, MatType.CV_8UC1, Scalar.White));
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Mask"] = mask;

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Mask");
    }

    [Fact]
    public void ValidateParameters_WithInvalidChannel_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "LAB" });
        sut.ValidateParameters(op).IsValid.Should().BeFalse();
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
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
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
