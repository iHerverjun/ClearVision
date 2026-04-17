using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Generic;

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

    [Fact]
    public async Task ExecuteAsync_WithSteger_ShouldReturnEdgesAndExposeSigmaUsed()
    {
        var op = new Operator("Subpixel", OperatorType.SubpixelEdgeDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "Steger", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Sigma", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("EdgeThreshold", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("LowThreshold", 10.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("HighThreshold", 30.0, "double"));

        using var image = TestHelpers.CreateGrayShapeTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Method");
        result.OutputData.Should().ContainKey("SigmaUsed");
        result.OutputData.Should().ContainKey("Edges");
        result.OutputData!["Method"].Should().Be("Steger");
        Convert.ToDouble(result.OutputData["SigmaUsed"]).Should().BeApproximately(2.0, 1e-9);
        ((IReadOnlyCollection<Dictionary<string, object>>)result.OutputData["Edges"]).Should().NotBeEmpty();
    }
}
