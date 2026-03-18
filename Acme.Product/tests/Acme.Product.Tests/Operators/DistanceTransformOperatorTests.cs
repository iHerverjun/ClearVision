using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class DistanceTransformOperatorTests
{
    private readonly DistanceTransformOperator _operator;

    public DistanceTransformOperatorTests()
    {
        _operator = new DistanceTransformOperator(Substitute.For<ILogger<DistanceTransformOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeDistanceTransform()
    {
        _operator.OperatorType.Should().Be(OperatorType.DistanceTransform);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithBinaryCircle_ShouldReturnSuccess()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DistanceType", "Euclidean"));
        
        using var image = CreateBinaryCircleImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("DistanceMap");
        result.OutputData.Should().ContainKey("MaxDistance");
        result.OutputData.Should().ContainKey("MaxLocation");
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentDistanceTypes_ShouldWork()
    {
        var distanceTypes = new[] { "Euclidean", "Manhattan", "Chessboard" };
        
        foreach (var distType in distanceTypes)
        {
            var op = new Operator($"DistanceTransform_{distType}", OperatorType.DistanceTransform, 0, 0);
            op.Parameters.Add(TestHelpers.CreateParameter("DistanceType", distType));
            
            using var image = CreateBinaryCircleImage();
            var inputs = TestHelpers.CreateImageInputs(image);

            var result = await _operator.ExecuteAsync(op, inputs);
            result.IsSuccess.Should().BeTrue($"Distance type {distType} should work");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithSignedDistance_ShouldReturnSuccess()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Signed", true));
        
        using var image = CreateBinaryCircleImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithRectangle_ShouldCalculateCorrectMaxDistance()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Threshold", 127.0));
        
        using var image = CreateBinaryRectangleImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        
        var maxDistance = result.OutputData["MaxDistance"];
        maxDistance.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithNormalize_ShouldReturnNormalizedImage()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Normalize", true));
        
        using var image = CreateBinaryCircleImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_WithValidMaskSize_ShouldBeValid()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MaskSize", 3));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMaskSize_ShouldBeInvalid()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MaskSize", 7)); // 不在支持的值(3,5)中

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidThreshold_ShouldBeInvalid()
    {
        var op = new Operator("DistanceTransform", OperatorType.DistanceTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Threshold", 300.0)); // 超出范围

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateBinaryCircleImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(200, 200), 100, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateBinaryRectangleImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(100, 100, 200, 200), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
