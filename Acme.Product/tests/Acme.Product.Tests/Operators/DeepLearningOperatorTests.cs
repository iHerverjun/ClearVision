// DeepLearningOperatorTests.cs
// 深度学习算子测试
// 作者：蘅芜君

using System.Reflection;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.ML.OnnxRuntime.Tensors;
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

    [Fact]
    public void ResolveLabelsPath_ShouldPreferLabelsPathAndFallbackToLegacyLabelFile()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "ResolveLabelsPath",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var withLabelsPath = CreateTestOperator("model.onnx", 0.5f);
        withLabelsPath.AddParameter(new Parameter(Guid.NewGuid(), "LabelsPath", "标签路径", string.Empty, "file", "C:\\labels\\active.txt"));
        withLabelsPath.AddParameter(new Parameter(Guid.NewGuid(), "LabelFile", "旧标签路径", string.Empty, "file", "C:\\labels\\legacy.txt"));

        var preferLabelsPath = method!.Invoke(null, new object?[] { withLabelsPath });
        preferLabelsPath.Should().Be("C:\\labels\\active.txt");

        var legacyOnly = CreateTestOperator("model.onnx", 0.5f);
        legacyOnly.AddParameter(new Parameter(Guid.NewGuid(), "LabelFile", "旧标签路径", string.Empty, "file", "C:\\labels\\legacy.txt"));

        var fallbackLegacy = method.Invoke(null, new object?[] { legacyOnly });
        fallbackLegacy.Should().Be("C:\\labels\\legacy.txt");
    }

    [Fact]
    public void BuildVisualizationDetections_ShouldApplyVisualOnlyNms_WhenInternalNmsIsDisabled()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "BuildVisualizationDetections",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var nestedType = typeof(DeepLearningOperator).GetNestedType("DetectionResult", BindingFlags.NonPublic);
        nestedType.Should().NotBeNull();
        var listType = typeof(List<>).MakeGenericType(nestedType!);
        var detections = Activator.CreateInstance(listType).Should().BeAssignableTo<System.Collections.IList>().Subject;
        detections.Add(CreateInnerDetectionResult(10f, 10f, 40f, 40f, 0.95f, 0));
        detections.Add(CreateInnerDetectionResult(12f, 12f, 40f, 40f, 0.85f, 0));
        detections.Add(CreateInnerDetectionResult(80f, 80f, 20f, 20f, 0.80f, 0));

        var result = method!.Invoke(_operator, new object?[] { detections, 0.05f, false });

        result.Should().BeAssignableTo<System.Collections.IEnumerable>();
        result.As<System.Collections.IEnumerable>().Cast<object>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(3, "Object", "Objects: 3")]
    [InlineData(2, "Defect", "Defects: 2")]
    public void BuildStatisticsLabel_ShouldUseAsciiText(int count, string detectionMode, string expected)
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "BuildStatisticsLabel",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var label = method!.Invoke(null, new object?[] { count, detectionMode });
        label.Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WithNamedTargetClassesAndBundledLabels_ShouldContinuePastLabelResolution()
    {
        var op = CreateTestOperator("model.onnx", 0.5f);
        op.AddParameter(new Parameter(Guid.NewGuid(), "TargetClasses", "TargetClasses", string.Empty, "string", "Wire_Black,Wire_Blue"));
        var inputs = CreateTestInputs();

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("模型文件不存在");
        result.ErrorMessage.Should().NotContain("Failed to resolve TargetClasses");
    }

    [Fact]
    public void DetectYoloVersion_WithCustomClassCount_ShouldRecognizeYoloV5Layouts()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "DetectYoloVersion",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var standard = new DenseTensor<float>(new float[1 * 25200 * 7], new[] { 1, 25200, 7 });
        var transposed = new DenseTensor<float>(new float[1 * 7 * 25200], new[] { 1, 7, 25200 });

        method!.Invoke(_operator, new object?[] { standard, 2 }).Should().Be(YoloVersion.YOLOv5);
        method.Invoke(_operator, new object?[] { transposed, 2 }).Should().Be(YoloVersion.YOLOv5);
    }

    [Fact]
    public void PostprocessYoloV5V6_ShouldSupportTransposedOutput()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PostprocessYoloV5V6",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var tensor = new DenseTensor<float>(new float[56], new[] { 1, 7, 8 });
        tensor[0, 0, 0] = 100f;
        tensor[0, 1, 0] = 100f;
        tensor[0, 2, 0] = 20f;
        tensor[0, 3, 0] = 20f;
        tensor[0, 4, 0] = 0.9f;
        tensor[0, 5, 0] = 0.1f;
        tensor[0, 6, 0] = 0.95f;

        var result = method!.Invoke(_operator, new object?[] { tensor, 0.5f, 200, 200, 200, false });

        result.Should().BeAssignableTo<System.Collections.IEnumerable>();
        var detections = result.As<System.Collections.IEnumerable>().Cast<object>().ToList();
        detections.Should().HaveCount(1);
        detections[0].GetType().GetProperty("ClassId")!.GetValue(detections[0]).Should().Be(1);
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

    private static object CreateInnerDetectionResult(float x, float y, float width, float height, float confidence, int classId)
    {
        var type = typeof(DeepLearningOperator).GetNestedType("DetectionResult", BindingFlags.NonPublic);
        type.Should().NotBeNull();

        var instance = Activator.CreateInstance(type!);
        type!.GetProperty("X")!.SetValue(instance, x);
        type.GetProperty("Y")!.SetValue(instance, y);
        type.GetProperty("Width")!.SetValue(instance, width);
        type.GetProperty("Height")!.SetValue(instance, height);
        type.GetProperty("Confidence")!.SetValue(instance, confidence);
        type.GetProperty("ClassId")!.SetValue(instance, classId);
        return instance!;
    }

    #endregion
}
