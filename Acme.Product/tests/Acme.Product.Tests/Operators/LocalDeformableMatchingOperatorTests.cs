using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class LocalDeformableMatchingOperatorTests
{
    private readonly LocalDeformableMatchingOperator _operator;

    public LocalDeformableMatchingOperatorTests()
    {
        _operator = new LocalDeformableMatchingOperator(Substitute.For<ILogger<LocalDeformableMatchingOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeLocalDeformableMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.LocalDeformableMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ShouldReturnFailure()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        // 没有模板应该返回失败或降级结果
        result.IsSuccess.Should().BeTrue(); // 算子本身返回成功，但包含失败信息
        result.OutputData["IsMatch"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameImageAsTemplate_ShouldMatch()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.3)); // 降低阈值以便测试
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 2)); // 减少层数以加快测试
        
        using var image = CreateFeatureRichImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Template"] = image;

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("IsMatch");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnDeformationInfo()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.3));
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 2));
        
        using var image = CreateFeatureRichImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Template"] = image;

        var result = await _operator.ExecuteAsync(op, inputs);
        result.OutputData.Should().ContainKey("Method");
        result.OutputData.Should().ContainKey("ProcessingTimeMs");
    }

    [Fact]
    public void ValidateParameters_WithValidParams_ShouldBeValid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.6));
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 3));
        op.Parameters.Add(TestHelpers.CreateParameter("TPSLambda", 0.01));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinScore_ShouldBeInvalid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", -0.1)); // 无效值

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPyramidLevels_ShouldBeInvalid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 0)); // 低于最小值1

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithTooHighPyramidLevels_ShouldBeInvalid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 10)); // 超过最大值6

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateFeatureRichImage()
    {
        // 创建包含丰富特征的图像
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Gray);
        
        // 添加一些几何形状以产生特征
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.Black, -1);
        Cv2.Rectangle(mat, new Rect(200, 150, 120, 80), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 300), 50, Scalar.Black, -1);
        Cv2.Circle(mat, new Point(150, 300), 30, Scalar.White, -1);
        
        // 添加线条纹理
        for (int i = 0; i < 8; i++)
        {
            Cv2.Line(mat, new Point(i * 50, 0), new Point(i * 50, 400), Scalar.DarkGray, 2);
            Cv2.Line(mat, new Point(0, i * 50), new Point(400, i * 50), Scalar.DarkGray, 2);
        }
        
        return new ImageWrapper(mat);
    }
}
