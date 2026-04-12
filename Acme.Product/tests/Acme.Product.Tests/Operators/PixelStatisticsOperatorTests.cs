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
