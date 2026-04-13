using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class FilteringOperatorTests
{
    private readonly GaussianBlurOperator _operator =
        new(Substitute.For<ILogger<GaussianBlurOperator>>());

    [Fact]
    public void OperatorType_ShouldMapToFiltering()
    {
        _operator.OperatorType.Should().Be(OperatorType.Filtering);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnBlurredImage()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 5, "int"));
        op.AddParameter(TestHelpers.CreateParameter("SigmaX", 1.2, "double"));

        using var image = TestHelpers.CreateShapeTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Width.Should().Be(400);
        outputImage.Height.Should().Be(400);
        result.OutputData["Width"].Should().Be(400);
        result.OutputData["Height"].Should().Be(400);
    }

    [Fact]
    public void ValidateParameters_WithUpperBoundKernelSize_ShouldBeValid()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("KernelSize", 31, "int"));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImage_ShouldReturnFailure()
    {
        var result = await _operator.ExecuteAsync(CreateOperator(), new Dictionary<string, object>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    private static Operator CreateOperator()
    {
        return new Operator("Filtering", OperatorType.Filtering, 0, 0);
    }
}
