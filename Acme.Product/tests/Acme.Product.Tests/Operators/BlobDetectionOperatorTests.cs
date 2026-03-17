// BlobDetectionOperatorTests.cs
// BlobDetectionOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class BlobDetectionOperatorTests
{
    private readonly BlobDetectionOperator _operator;

    public BlobDetectionOperatorTests()
    {
        _operator = new BlobDetectionOperator(Substitute.For<ILogger<BlobDetectionOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeBlobAnalysis()
    {
        _operator.OperatorType.Should().Be(OperatorType.BlobAnalysis);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMinRectangularity_ShouldFilterOutRoundBlobs()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinRectangularity", 0.9, "double"));

        using var image = TestHelpers.CreateShapeTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        Convert.ToInt32(result.OutputData!["BlobCount"]).Should().Be(1);

        var blobs = result.OutputData["Blobs"].Should().BeOfType<List<Dictionary<string, object>>>().Subject;
        blobs.Should().HaveCount(1);
        Convert.ToDouble(blobs[0]["Rectangularity"]).Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task ExecuteAsync_WithSyntheticCircle_ShouldHaveHighCircularity()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MaxArea", 300000, "int"));

        using var mat = new Mat(512, 512, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(256, 256), 200, Scalar.White, -1);

        var inputs = TestHelpers.CreateImageInputs(new ImageWrapper(mat));
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        Convert.ToInt32(result.OutputData!["BlobCount"]).Should().Be(1);

        var blobs = result.OutputData["Blobs"].Should().BeOfType<List<Dictionary<string, object>>>().Subject;
        Convert.ToDouble(blobs[0]["Circularity"]).Should().BeGreaterThan(0.99);
    }

    [Fact]
    public async Task ExecuteAsync_WithSyntheticRectangle_ShouldHaveHighRectangularity()
    {
        var op = new Operator("测试", OperatorType.BlobAnalysis, 0, 0);

        using var mat = new Mat(512, 512, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(156, 206, 200, 100), Scalar.White, -1);

        var inputs = TestHelpers.CreateImageInputs(new ImageWrapper(mat));
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        Convert.ToInt32(result.OutputData!["BlobCount"]).Should().Be(1);

        var blobs = result.OutputData["Blobs"].Should().BeOfType<List<Dictionary<string, object>>>().Subject;
        Convert.ToDouble(blobs[0]["Rectangularity"]).Should().BeGreaterThan(0.95);
    }
}
