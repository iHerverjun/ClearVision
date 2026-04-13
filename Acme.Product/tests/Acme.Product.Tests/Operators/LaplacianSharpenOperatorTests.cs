using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class LaplacianSharpenOperatorTests
{
    private readonly LaplacianSharpenOperator _operator =
        new(Substitute.For<ILogger<LaplacianSharpenOperator>>());

    [Fact]
    public void OperatorType_ShouldBeLaplacianSharpen()
    {
        _operator.OperatorType.Should().Be(OperatorType.LaplacianSharpen);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSharpenedImageAndMetadata()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 3, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Scale", 1.5, "double"));
        op.AddParameter(TestHelpers.CreateParameter("SharpenStrength", 1.2, "double"));

        using var image = TestHelpers.CreateGradientTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Width.Should().Be(200);
        outputImage.Height.Should().Be(200);
        result.OutputData["KernelSize"].Should().Be(3);
        result.OutputData["Scale"].Should().Be(1.5);
        result.OutputData["SharpenStrength"].Should().Be(1.2);
    }

    [Fact]
    public async Task ExecuteAsync_WithEvenKernelSize_ShouldNormalizeToOddKernel()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 4, "int"));

        using var image = TestHelpers.CreateGradientTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["KernelSize"].Should().Be(5);
    }

    [Fact]
    public void ValidateParameters_WithKernelSizeOutOfRange_ShouldReturnInvalid()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 9, "int"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static Operator CreateOperator()
    {
        return new Operator("LaplacianSharpen", OperatorType.LaplacianSharpen, 0, 0);
    }
}
