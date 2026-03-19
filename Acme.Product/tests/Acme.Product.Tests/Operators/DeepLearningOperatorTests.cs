// DeepLearningOperatorTests.cs
// 深度学习算子测试
// 作者：蘅芜君

using System.Reflection;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

/// <summary>
/// 深度学习算子测试
/// 支持多种 YOLO 版本：YOLOv5, YOLOv6, YOLOv8, YOLOv11
/// </summary>
public class DeepLearningOperatorTests
{
    private readonly DeepLearningOperator _operator;

    public DeepLearningOperatorTests()
    {
        _operator = new DeepLearningOperator(Substitute.For<ILogger<DeepLearningOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldReturnDeepLearning()
    {
        // Assert
        _operator.OperatorType.Should().Be(OperatorType.DeepLearning);
    }

    #region 参数验证测试

    [Theory]
    [InlineData("model.onnx", 0.5f, "YOLOv8", true)]
    [InlineData("model.onnx", 0.5f, "YOLOv11", true)]
    [InlineData("model.onnx", 0.5f, "YOLOv6", true)]
    [InlineData("model.onnx", 0.5f, "YOLOv5", true)]
    [InlineData("model.onnx", 0.5f, "Auto", true)]
    [InlineData("", 0.5f, "YOLOv8", false)]
    [InlineData("model.onnx", 1.5f, "YOLOv8", false)]
    [InlineData("model.onnx", -0.1f, "YOLOv8", false)]
    public void ValidateParameters_WithVariousInputs_ShouldReturnExpectedResult(
        string modelPath, float confidence, string version, bool expectedValid)
    {
        // Arrange
        var op = CreateTestOperator(modelPath, confidence, version);

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void ParseTargetClasses_WithCustomLabels_ShouldPreferLoadedLabels()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "ParseTargetClasses",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var labels = new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" };
        var result = method!.Invoke(_operator, new object?[] { "Wire_Brown,Wire_Blue", labels });

        result.Should().BeAssignableTo<HashSet<int>>();
        result.As<HashSet<int>>().Should().BeEquivalentTo(new[] { 0, 2 });
    }

    #endregion

    #region YOLO 版本支持测试

    [Theory]
    [InlineData("YOLOv5")]
    [InlineData("YOLOv6")]
    [InlineData("YOLOv8")]
    [InlineData("YOLOv11")]
    [InlineData("Auto")]
    [InlineData("v5")]
    [InlineData("v6")]
    [InlineData("v8")]
    [InlineData("v11")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("8")]
    [InlineData("11")]
    public void ExecuteAsync_WithDifferentYoloVersions_ShouldAcceptAllFormats(string version)
    {
        // Arrange
        var op = CreateTestOperator("non_existent.onnx", 0.5f, version);
        var inputs = CreateTestInputs();

        // Act
        // 这里不会真的执行模型，因为模型不存在
        // 我们只是想验证参数解析不会报错
        var exception = Record.Exception(() => _operator.ValidateParameters(op));

        // Assert
        exception.Should().BeNull();
    }

    #endregion

    #region 错误处理测试

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        // Act
        var op = CreateTestOperator("model.onnx", 0.5f);
        var result = await _operator.ExecuteAsync(op, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未提供输入图像");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImage_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator("model.onnx", 0.5f);
        var inputs = new Dictionary<string, object>();

        // Act
        var result = await _operator.ExecuteAsync(op, inputs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未提供输入图像");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyModelPath_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator(string.Empty, 0.5f);
        var inputs = CreateTestInputs();

        // Act
        var result = await _operator.ExecuteAsync(op, inputs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未指定模型路径");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentModel_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator("non_existent_model.onnx", 0.5f);
        var inputs = CreateTestInputs();

        // Act
        var result = await _operator.ExecuteAsync(op, inputs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("模型文件不存在");
    }

    #endregion

    #region Helper Methods

    private static Operator CreateTestOperator(string modelPath, float confidence, string? version = null)
    {
        var op = new Operator("深度学习测试", OperatorType.DeepLearning, 0, 0);

        if (!string.IsNullOrEmpty(modelPath))
        {
            op.AddParameter(new Parameter(
                Guid.NewGuid(),
                "ModelPath",
                "模型路径",
                "ONNX模型文件路径",
                "file",
                modelPath,
                null,
                null,
                true,
                null
            ));
        }

        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "Confidence",
            "置信度阈值",
            "检测置信度阈值",
            "double",
            confidence,
            0.0,
            1.0,
            true,
            null
        ));

        // 添加版本参数
        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "ModelVersion",
            "YOLO版本",
            "YOLO模型版本",
            "enum",
            version ?? "Auto",
            null,
            null,
            true,
            null
        ));

        // 添加输入尺寸参数
        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "InputSize",
            "输入尺寸",
            "模型输入尺寸",
            "int",
            640,
            320,
            1280,
            true,
            null
        ));

        return op;
    }

    private static Dictionary<string, object> CreateTestInputs()
    {
        var imageData = CreateTestImage();

        return new Dictionary<string, object>
        {
            { "Image", imageData }
        };
    }

    private static byte[] CreateTestImage()
    {
        // 创建一个简单的 100x100 红色 PNG 图像
        using var mat = new OpenCvSharp.Mat(100, 100, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(0, 0, 255));
        return mat.ToBytes(".png");
    }

    #endregion
}
