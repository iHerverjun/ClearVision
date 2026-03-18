using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MinEnclosingGeometryOperatorTests
{
    private readonly MinEnclosingGeometryOperator _operator;

    public MinEnclosingGeometryOperatorTests()
    {
        _operator = new MinEnclosingGeometryOperator(Substitute.For<ILogger<MinEnclosingGeometryOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeMinEnclosingGeometry()
    {
        _operator.OperatorType.Should().Be(OperatorType.MinEnclosingGeometry);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithCircleImage_ShouldDetectCircle()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Operation", "SmallestCircle"));
        
        // 创建圆形测试图像
        using var image = CreateCircleTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("GeometryResult");
    }

    [Fact]
    public async Task ExecuteAsync_WithRectangleImage_ShouldDetectRectangle()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Operation", "MinAreaRect"));
        
        using var image = CreateRectangleTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithRobustCircleFit_ShouldReturnSuccess()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Operation", "FitCircleRobust"));
        
        using var image = CreateCircleTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithConvexHull_ShouldReturnSuccess()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("Operation", "ConvexHull"));
        
        using var image = TestHelpers.CreateShapeTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithValidRansacParams_ShouldBeValid()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("RansacIterations", 500));
        op.Parameters.Add(TestHelpers.CreateParameter("RansacInlierThreshold", 2.0));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidRansacIterations_ShouldBeInvalid()
    {
        var op = new Operator("MinEnclosingGeometry", OperatorType.MinEnclosingGeometry, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("RansacIterations", 5)); // 小于最小值10

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateCircleTestImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(200, 200), 100, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateRectangleTestImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(100, 100, 200, 150), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
