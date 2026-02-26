using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class SubpixelEdgeDetectionOperatorTests
{
    private readonly SubpixelEdgeDetectionOperator _operator;

    public SubpixelEdgeDetectionOperatorTests()
    {
        _operator = new SubpixelEdgeDetectionOperator(Substitute.For<ILogger<SubpixelEdgeDetectionOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeSubpixelEdgeDetection()
    {
        _operator.OperatorType.Should().Be(OperatorType.SubpixelEdgeDetection);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("Subpixel", OperatorType.SubpixelEdgeDetection, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("Subpixel", OperatorType.SubpixelEdgeDetection, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithGradientInterp_ShouldReturnEdges()
    {
        var op = new Operator("Subpixel", OperatorType.SubpixelEdgeDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "GradientInterp", "string"));

        using var image = TestHelpers.CreateShapeTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Edges");
        result.OutputData.Should().ContainKey("Method");
        result.OutputData!["Method"].Should().Be("GradientInterp");
    }
}
