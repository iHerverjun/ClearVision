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
public class HistogramAnalysisOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeHistogramAnalysis()
    {
        CreateSut().OperatorType.Should().Be(OperatorType.HistogramAnalysis);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnIntensityDomainStatistics()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Channel"] = "Gray",
            ["BinCount"] = 64
        });

        using var image = CreateGradientImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var mode = Convert.ToDouble(result.OutputData!["Mode"]);
        var median = Convert.ToDouble(result.OutputData["Median"]);
        mode.Should().BeGreaterThanOrEqualTo(0.0);
        median.Should().BeGreaterThanOrEqualTo(0.0);
        result.OutputData.Should().ContainKeys("ModeBinIndex", "MedianBinIndex", "PeakBinIndex", "ValleyBinIndex");
    }

    [Fact]
    public async Task ExecuteAsync_DiscreteDistribution_ShouldReturnExactBinStatistics()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Channel"] = "Gray",
            ["BinCount"] = 256
        });

        using var image = CreateDiscreteDistributionImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Mean"]).Should().BeApproximately(110.8, 1e-6);
        Convert.ToDouble(result.OutputData["Mode"]).Should().BeApproximately(60.0, 1e-6);
        Convert.ToDouble(result.OutputData["Median"]).Should().BeApproximately(140.0, 1e-6);
        Convert.ToDouble(result.OutputData["Peak"]).Should().BeApproximately(60.0, 1e-6);
        Convert.ToInt32(result.OutputData["ModeBinIndex"]).Should().Be(60);
        Convert.ToInt32(result.OutputData["MedianBinIndex"]).Should().Be(140);
        Convert.ToInt32(result.OutputData["ValleyBinIndex"]).Should().BeGreaterThan(60).And.BeLessThan(140);
        Convert.ToDouble(result.OutputData["BinWidth"]).Should().BeApproximately(1.0, 1e-9);
        Convert.ToDouble(result.OutputData["ModeCount"]).Should().BeApproximately(29.0, 1e-6);
        Convert.ToDouble(result.OutputData["HistogramMass"]).Should().BeApproximately(100.0, 1e-6);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeApproximately(1.0 / Math.Sqrt(12.0), 1e-9);
    }

    [Fact]
    public void ValidateParameters_WithInvalidChannel_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["Channel"] = "HSV" });
        sut.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static HistogramAnalysisOperator CreateSut()
    {
        return new HistogramAnalysisOperator(Substitute.For<ILogger<HistogramAnalysisOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("HistogramAnalysis", OperatorType.HistogramAnalysis, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateGradientImage()
    {
        var mat = new Mat(80, 160, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                var v = (byte)(x * 255 / mat.Cols);
                mat.Set(y, x, new Vec3b(v, v, v));
            }
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateDiscreteDistributionImage()
    {
        var values = Enumerable.Repeat((byte)10, 20)
            .Concat(Enumerable.Repeat((byte)60, 29))
            .Concat(Enumerable.Repeat((byte)140, 26))
            .Concat(Enumerable.Repeat((byte)220, 25))
            .ToArray();

        var mat = new Mat(10, 10, MatType.CV_8UC3);
        for (var index = 0; index < values.Length; index++)
        {
            var y = index / 10;
            var x = index % 10;
            var value = values[index];
            mat.Set(y, x, new Vec3b(value, value, value));
        }

        return new ImageWrapper(mat);
    }
}
