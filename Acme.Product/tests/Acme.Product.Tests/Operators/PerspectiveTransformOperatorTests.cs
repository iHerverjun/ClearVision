using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class PerspectiveTransformOperatorTests
{
    private readonly PerspectiveTransformOperator _operator;

    public PerspectiveTransformOperatorTests()
    {
        _operator = new PerspectiveTransformOperator(Substitute.For<ILogger<PerspectiveTransformOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePerspectiveTransform()
    {
        _operator.OperatorType.Should().Be(OperatorType.PerspectiveTransform);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithPointSetJson_ShouldPreferPointSetMode()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SrcPointsJson", "SrcPointsJson", string.Empty, "string", "[[0,0],[180,0],[180,180],[0,180]]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "DstPointsJson", "DstPointsJson", string.Empty, "string", "[[10,10],[190,20],[180,190],[20,180]]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OutputWidth", "OutputWidth", string.Empty, "int", 200));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OutputHeight", "OutputHeight", string.Empty, "int", 200));

        using var image = TestHelpers.CreateTestImage(200, 200);
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("PointSetMode");
        result.OutputData!["PointSetMode"].Should().Be("PointSetJsonOrInput");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonCyclicPointOrder_ShouldNotBeTreatedAsDegenerate()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SrcPointsJson", "SrcPointsJson", string.Empty, "string", "[[0,0],[180,0],[0,180],[180,180]]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "DstPointsJson", "DstPointsJson", string.Empty, "string", "[[10,10],[190,10],[10,190],[190,190]]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OutputWidth", "OutputWidth", string.Empty, "int", 200));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OutputHeight", "OutputHeight", string.Empty, "int", 200));

        using var image = TestHelpers.CreateTestImage(200, 200);
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPointJson_ShouldFailClosed()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SrcPointsJson", "SrcPointsJson", string.Empty, "string", "[{\"x\":0},{\"x\":180,\"y\":0},{\"x\":180,\"y\":180},{\"x\":0,\"y\":180}]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "DstPointsJson", "DstPointsJson", string.Empty, "string", "[[0,0],[200,0],[200,200],[0,200]]"));

        using var image = TestHelpers.CreateTestImage(200, 200);
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SrcPoints");
    }

    [Fact]
    public async Task ExecuteAsync_WithDegeneratePointSet_ShouldFail()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SrcPointsJson", "SrcPointsJson", string.Empty, "string", "[[0,0],[50,0],[100,0],[150,0]]"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "DstPointsJson", "DstPointsJson", string.Empty, "string", "[[0,0],[200,0],[200,200],[0,200]]"));

        using var image = TestHelpers.CreateTestImage(200, 200);
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("退化");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.PerspectiveTransform, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
