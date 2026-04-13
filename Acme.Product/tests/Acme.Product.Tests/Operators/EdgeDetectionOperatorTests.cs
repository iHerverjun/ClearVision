using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class EdgeDetectionOperatorTests
{
    private readonly CannyEdgeOperator _operator =
        new(Substitute.For<ILogger<CannyEdgeOperator>>());

    [Fact]
    public void OperatorType_ShouldMapToEdgeDetection()
    {
        _operator.OperatorType.Should().Be(OperatorType.EdgeDetection);
    }

    [Fact]
    public async Task ExecuteAsync_WithShapeImage_ShouldReturnEdgeOutputs()
    {
        using var image = TestHelpers.CreateShapeTestImage();
        var result = await _operator.ExecuteAsync(CreateOperator(), TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("Edges");
        result.OutputData!["Edges"].Should().BeOfType<byte[]>();
        result.OutputData["AutoThreshold"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithAutoThreshold_ShouldExposeThresholdsUsed()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("AutoThreshold", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("AutoThresholdSigma", 0.5, "double"));
        op.AddParameter(TestHelpers.CreateParameter("GaussianKernelSize", 4, "int"));

        using var image = TestHelpers.CreateGradientTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToBoolean(result.OutputData!["AutoThreshold"]).Should().BeTrue();
        Convert.ToDouble(result.OutputData["Threshold2Used"])
            .Should().BeGreaterThan(Convert.ToDouble(result.OutputData["Threshold1Used"]));
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
        return new Operator("EdgeDetection", OperatorType.EdgeDetection, 0, 0);
    }
}
