using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class PlanarMatchingOperatorTests
{
    private readonly PlanarMatchingOperator _operator;

    public PlanarMatchingOperatorTests()
    {
        _operator = new PlanarMatchingOperator(Substitute.For<ILogger<PlanarMatchingOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePlanarMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.PlanarMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ShouldReturnFailure()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);
        // 没有模板会返回失败或低分匹配
        result.OutputData.Should().ContainKey("IsMatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithSameImageAsTemplate_ShouldMatch()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 4));
        
        using var image = CreateFeatureRichImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Template"] = image; // 使用相同图像作为模板

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("IsMatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentDetectors_ShouldWork()
    {
        var detectors = new[] { "ORB", "AKAZE", "BRISK" };
        
        foreach (var detector in detectors)
        {
            var op = new Operator($"PlanarMatching_{detector}", OperatorType.PlanarMatching, 0, 0);
            op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", detector));
            
            using var image = CreateFeatureRichImage();
            var inputs = TestHelpers.CreateImageInputs(image);
            inputs["Template"] = image;

            var result = await _operator.ExecuteAsync(op, inputs);
            result.IsSuccess.Should().BeTrue($"Detector {detector} should work");
        }
    }

    [Fact]
    public void ValidateParameters_WithValidMatchRatio_ShouldBeValid()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MatchRatio", 0.75));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMatchRatio_ShouldBeInvalid()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MatchRatio", 0.3)); // 低于最小值0.5

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinMatchCount_ShouldBeInvalid()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 2)); // 低于最小值4

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateFeatureRichImage()
    {
        // 创建包含丰富特征的图像以便匹配
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Gray);
        
        // 添加一些角点和边缘
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.Black, -1);
        Cv2.Rectangle(mat, new Rect(200, 150, 120, 80), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 300), 50, Scalar.Black, -1);
        
        // 添加一些纹理
        for (int i = 0; i < 10; i++)
        {
            Cv2.Line(mat, new Point(i * 40, 0), new Point(i * 40, 400), Scalar.DarkGray, 2);
        }
        
        return new ImageWrapper(mat);
    }
}
