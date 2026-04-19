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
    public void TryResolveBundledLabelsPath_WithoutNamedTargetClasses_ShouldNotReturnBundledLabels()
    {
        var resolverType = typeof(DeepLearningOperator).Assembly.GetType("Acme.Product.Infrastructure.Services.DeepLearningLabelResolver");
        resolverType.Should().NotBeNull();

        var method = resolverType!.GetMethod("TryResolveBundledLabelsPath", BindingFlags.Static | BindingFlags.Public, new[] { typeof(string) });
        method.Should().NotBeNull();

        var result = method!.Invoke(null, new object?[] { string.Empty });
        result.Should().BeNull();
    }

    [Fact]
    public void AreLabelsResolvable_WithoutTargetClassesAndWithoutMetadataOrFiles_ShouldReturnFalse()
    {
        var isResolvable = InvokeAreLabelsResolvable(
            explicitLabelPath: string.Empty,
            modelPath: string.Empty,
            targetClassesStr: string.Empty,
            out var resolvedPath);

        isResolvable.Should().BeFalse();
        resolvedPath.Should().BeNull();
    }

    [Fact]
    public void AreLabelsResolvable_WithExplicitLabelsFile_ShouldReturnTrue()
    {
        var labelsPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(labelsPath, new[] { "class_a", "class_b" });

            var isResolvable = InvokeAreLabelsResolvable(
                explicitLabelPath: labelsPath,
                modelPath: string.Empty,
                targetClassesStr: string.Empty,
                out var resolvedPath);

            isResolvable.Should().BeTrue();
            resolvedPath.Should().Be(labelsPath);
        }
        finally
        {
            if (File.Exists(labelsPath))
            {
                File.Delete(labelsPath);
            }
        }
    }

    [Fact]
    public void AreLabelsResolvable_WithNamedTargetClassesAndBundledLabels_ShouldReturnTrue()
    {
        var isResolvable = InvokeAreLabelsResolvable(
            explicitLabelPath: string.Empty,
            modelPath: string.Empty,
            targetClassesStr: "Wire_Black,Wire_Blue",
            out var resolvedPath);

        isResolvable.Should().BeTrue();
        resolvedPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseMetadataNames_ShouldSupportUltralyticsMetadataFormat()
    {
        var labels = InvokeParseMetadataNames("{0: 'Wire_Blue', 1: 'Wire_Black'}");

        labels.Should().Equal("Wire_Blue", "Wire_Black");
    }

    [Fact]
    public void BuildLabelContract_WithMatchingMetadataAndExternalLabels_ShouldPreferMetadataLabels()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "BuildLabelContract",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var sourceInfo = CreateLabelSourceInfo(
            new[] { "Wire_Blue", "Wire_Black" },
            "ExplicitFile",
            "C:\\labels\\wire.txt",
            isFileBacked: true);

        var result = method!.Invoke(_operator, new object?[] { "model.onnx", new[] { "Wire_Blue", "Wire_Black" }, sourceInfo });

        result.Should().NotBeNull();
        GetPropertyValue<bool>(result!, "IsValid").Should().BeTrue();
        GetPropertyValue<string[]>(result!, "ResolvedLabels").Should().Equal("Wire_Blue", "Wire_Black");
        GetPropertyValue<string>(result!, "ResolvedLabelSource").Should().Be("ModelMetadata");
        GetPropertyValue<string>(result!, "ValidationStatus").Should().Be("MetadataValidatedWithExternalLabels");
    }

    [Fact]
    public void BuildLabelContract_WithMismatchedMetadataAndExternalLabels_ShouldFailFast()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "BuildLabelContract",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var sourceInfo = CreateLabelSourceInfo(
            new[] { "Wire_Black", "Wire_Blue" },
            "ExplicitFile",
            "C:\\labels\\wire.txt",
            isFileBacked: true);

        var result = method!.Invoke(_operator, new object?[] { "model.onnx", new[] { "Wire_Blue", "Wire_Black" }, sourceInfo });

        result.Should().NotBeNull();
        GetPropertyValue<bool>(result!, "IsValid").Should().BeFalse();
        GetPropertyValue<string>(result!, "ValidationStatus").Should().Be("Mismatch");
        GetPropertyValue<string>(result!, "ValidationMessage").Should().Contain("Label contract mismatch");
        GetPropertyValue<string>(result!, "ValidationMessage").Should().Contain("ModelMetadataLabels: Wire_Blue, Wire_Black");
        GetPropertyValue<string>(result!, "ValidationMessage").Should().Contain("ExternalLabels: Wire_Black, Wire_Blue");
    }

    [Fact]
    public void BuildLabelContract_WithoutMetadataOrExternalLabels_ShouldFailFast()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "BuildLabelContract",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var sourceInfo = CreateLabelSourceInfo(Array.Empty<string>(), "Unavailable", string.Empty, isFileBacked: false);

        var result = method!.Invoke(_operator, new object?[] { "model.onnx", Array.Empty<string>(), sourceInfo });

        result.Should().NotBeNull();
        GetPropertyValue<bool>(result!, "IsValid").Should().BeFalse();
        GetPropertyValue<string>(result!, "ValidationStatus").Should().Be("MissingLabelContract");
        GetPropertyValue<string>(result!, "ValidationMessage").Should().Contain("Label contract missing");
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

    [Fact]
    public void ApplyNmsWithStats_WithHeavyOverlap_ShouldKeepTopCandidateAndAvoidQuadraticComparisons()
    {
        var detections = Enumerable.Range(0, 800)
            .Select(i => CreateInnerDetectionResult(
                x: 100f + (i % 3) * 0.2f,
                y: 120f + (i % 3) * 0.2f,
                width: 64f,
                height: 64f,
                confidence: 0.99f - (i * 0.0005f),
                classId: 0))
            .ToList();

        var (kept, comparisons) = InvokeApplyNmsWithStats(detections, 0.45f);

        kept.Should().HaveCount(1);
        comparisons.Should().BeLessThan(2000);
    }

    [Fact]
    public void ApplyNmsWithStats_WithInvalidCandidates_ShouldDiscardInvalidBoxes()
    {
        var detections = new List<object>
        {
            CreateInnerDetectionResult(
                x: 12f,
                y: 12f,
                width: 0f,
                height: 25f,
                confidence: 0.99f,
                classId: 0),
            CreateInnerDetectionResult(
                x: 10f,
                y: 10f,
                width: 20f,
                height: 20f,
                confidence: 0.90f,
                classId: 0)
        };

        var (kept, _) = InvokeApplyNmsWithStats(detections, 0.45f);

        kept.Should().HaveCount(1);
        GetPropertyValue<float>(kept[0], "Width").Should().BeGreaterThan(0f);
        GetPropertyValue<float>(kept[0], "Height").Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ApplyNmsWithStats_WithLargeSeparatedCandidates_ShouldSignificantlyReduceIoUChecks()
    {
        var detections = new List<object>();
        const int rows = 20;
        const int cols = 20;
        const int spacing = 72;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                detections.Add(CreateInnerDetectionResult(
                    x: col * spacing,
                    y: row * spacing,
                    width: 10f,
                    height: 10f,
                    confidence: 0.95f,
                    classId: 0));
            }
        }

        var (kept, comparisons) = InvokeApplyNmsWithStats(detections, 0.45f);
        var naiveComparisons = kept.Count * (kept.Count - 1L) / 2L;

        kept.Should().HaveCount(rows * cols);
        comparisons.Should().BeLessThan(5000);
        comparisons.Should().BeLessThan(naiveComparisons / 10);
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
    public void GetClassName_WithoutResolvedLabels_ShouldNotFallbackToCoco()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "GetClassName",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var label = method!.Invoke(_operator, new object?[] { 0, Array.Empty<string>() });
        label.Should().Be("class_0");
    }

    [Fact]
    public void PreprocessImage_WithGrayscaleInput_ShouldProduceThreeChannelTensor()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var grayscale = new OpenCvSharp.Mat(32, 24, OpenCvSharp.MatType.CV_8UC1, new OpenCvSharp.Scalar(128));

        var tensor = method!.Invoke(_operator, new object?[] { grayscale, 64 });

        tensor.Should().BeAssignableTo<DenseTensor<float>>();
        tensor.As<DenseTensor<float>>().Dimensions.ToArray().Should().Equal(1, 3, 64, 64);
    }

    [Fact]
    public void PreprocessImage_WithFourChannelInput_ShouldProduceThreeChannelTensor()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var bgra = new OpenCvSharp.Mat(20, 18, OpenCvSharp.MatType.CV_8UC4, new OpenCvSharp.Scalar(10, 20, 30, 255));

        var tensor = method!.Invoke(_operator, new object?[] { bgra, 64 });

        tensor.Should().BeAssignableTo<DenseTensor<float>>();
        tensor.As<DenseTensor<float>>().Dimensions.ToArray().Should().Equal(1, 3, 64, 64);
    }

    [Fact]
    public void PreprocessImage_WithSixteenBitGrayscaleInput_ShouldProduceThreeChannelTensor()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var grayscale16 = new OpenCvSharp.Mat(32, 24, OpenCvSharp.MatType.CV_16UC1, new OpenCvSharp.Scalar(4096));

        var tensor = method!.Invoke(_operator, new object?[] { grayscale16, 64 });

        tensor.Should().BeAssignableTo<DenseTensor<float>>();
        tensor.As<DenseTensor<float>>().Dimensions.ToArray().Should().Equal(1, 3, 64, 64);
    }

    [Fact]
    public void PreprocessImage_WithSixteenBitThreeChannelInput_ShouldProduceThreeChannelTensor()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var bgr16 = new OpenCvSharp.Mat(20, 18, OpenCvSharp.MatType.CV_16UC3, new OpenCvSharp.Scalar(1024, 2048, 4096));

        var tensor = method!.Invoke(_operator, new object?[] { bgr16, 64 });

        tensor.Should().BeAssignableTo<DenseTensor<float>>();
        tensor.As<DenseTensor<float>>().Dimensions.ToArray().Should().Equal(1, 3, 64, 64);
    }

    [Fact]
    public void PreprocessImage_WithFloatUnitRangeInput_ShouldKeepExpectedScale()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var gray32 = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_32FC1, new OpenCvSharp.Scalar(0.5));

        var tensor = method!.Invoke(_operator, new object?[] { gray32, 64 }).Should().BeAssignableTo<DenseTensor<float>>().Subject;
        var values = tensor.ToArray();
        var channelSize = 64 * 64;
        values[0].Should().BeApproximately(0.5f, 0.02f);
        values[channelSize].Should().BeApproximately(0.5f, 0.02f);
        values[channelSize * 2].Should().BeApproximately(0.5f, 0.02f);
    }

    [Fact]
    public void PreprocessImage_WithFloatZeroTo255Input_ShouldUseDirectConversion()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var bgr32 = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_32FC3, new OpenCvSharp.Scalar(10, 20, 30));

        var tensor = method!.Invoke(_operator, new object?[] { bgr32, 64 }).Should().BeAssignableTo<DenseTensor<float>>().Subject;
        var values = tensor.ToArray();
        var channelSize = 64 * 64;
        values[0].Should().BeApproximately(30f / 255f, 0.02f);
        values[channelSize].Should().BeApproximately(20f / 255f, 0.02f);
        values[channelSize * 2].Should().BeApproximately(10f / 255f, 0.02f);
    }

    [Fact]
    public void PreprocessImage_WithOutOfRangeFloatInput_ShouldFailClosed()
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "PreprocessImage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        using var gray32 = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_32FC1, new OpenCvSharp.Scalar(-10.0));
        gray32.Set(0, 0, 10.0f);

        var action = () => method!.Invoke(_operator, new object?[] { gray32, 64 });

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*[0,1]*[0,255]*[0,65535]*");
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
    public void SelectDetectionOutputIndex_WithKnownLabelCount_ShouldPreferMatchingRank3Output()
    {
        var outputNames = new[] { "seg_output", "det_output" };
        var outputShapes = new[]
        {
            new[] { 1, 3, 64, 64 },
            new[] { 1, 84, 8400 }
        };

        var (selectedIndex, selectionRule) = InvokeSelectDetectionOutputIndex(outputNames, outputShapes, 80);

        selectedIndex.Should().Be(1);
        selectionRule.Should().Contain("KnownLabelFeature");
    }

    [Fact]
    public void SelectDetectionOutputIndex_WithoutKnownLabelCount_ShouldUseRank3Heuristic()
    {
        var outputNames = new[] { "small_rank3", "large_rank3" };
        var outputShapes = new[]
        {
            new[] { 1, 32, 32 },
            new[] { 1, 84, 8400 }
        };

        var (selectedIndex, selectionRule) = InvokeSelectDetectionOutputIndex(outputNames, outputShapes, 0);

        selectedIndex.Should().Be(1);
        selectionRule.Should().Be("Rank3Heuristic");
    }

    [Fact]
    public void SelectDetectionOutputIndex_WhenNoRank3Output_ShouldFailClosed()
    {
        var outputNames = new[] { "output0", "output1" };
        var outputShapes = new[]
        {
            new[] { 1, 3, 64, 64 },
            new[] { 1, 2, 32, 32 }
        };

        var action = () => InvokeSelectDetectionOutputIndex(outputNames, outputShapes, 0);

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*Could not identify*");
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

    private static object CreateLabelSourceInfo(string[] labels, string source, string path, bool isFileBacked)
    {
        var type = typeof(DeepLearningOperator).GetNestedType("LabelSourceInfo", BindingFlags.NonPublic);
        type.Should().NotBeNull();

        var instance = Activator.CreateInstance(type!);
        type!.GetProperty("Labels")!.SetValue(instance, labels);
        type.GetProperty("Source")!.SetValue(instance, source);
        type.GetProperty("Path")!.SetValue(instance, path);
        type.GetProperty("IsFileBacked")!.SetValue(instance, isFileBacked);
        return instance!;
    }

    private static T GetPropertyValue<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull();
        return (T)property!.GetValue(instance)!;
    }

    private static bool InvokeAreLabelsResolvable(
        string? explicitLabelPath,
        string? modelPath,
        string targetClassesStr,
        out string? resolvedPath)
    {
        var resolverType = typeof(DeepLearningOperator).Assembly.GetType("Acme.Product.Infrastructure.Services.DeepLearningLabelResolver");
        resolverType.Should().NotBeNull();

        var method = resolverType!.GetMethod(
            "AreLabelsResolvable",
            BindingFlags.Static | BindingFlags.Public);
        method.Should().NotBeNull();

        var args = new object?[] { explicitLabelPath, modelPath, targetClassesStr, null };
        var isResolvable = method!.Invoke(null, args).Should().BeOfType<bool>().Subject;
        resolvedPath = args[3]?.ToString();
        return isResolvable;
    }

    private static (int SelectedIndex, string SelectionRule) InvokeSelectDetectionOutputIndex(
        IReadOnlyList<string> outputNames,
        IReadOnlyList<int[]> outputShapes,
        int knownLabelCount)
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "SelectDetectionOutputIndex",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var tuple = method!.Invoke(null, new object?[] { outputNames, outputShapes, knownLabelCount });
        tuple.Should().NotBeNull();
        var tupleType = tuple!.GetType();
        var selectedIndex = (int)(tupleType.GetField("Item1")!.GetValue(tuple)!);
        var selectionRule = (string)(tupleType.GetField("Item2")!.GetValue(tuple)!);
        return (selectedIndex, selectionRule);
    }

    private (List<object> Kept, long Comparisons) InvokeApplyNmsWithStats(
        IReadOnlyList<object> detections,
        float iouThreshold)
    {
        var method = typeof(DeepLearningOperator).GetMethod(
            "ApplyNmsWithStats",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var nestedType = typeof(DeepLearningOperator).GetNestedType("DetectionResult", BindingFlags.NonPublic);
        nestedType.Should().NotBeNull();

        var listType = typeof(List<>).MakeGenericType(nestedType!);
        var typedDetections = Activator.CreateInstance(listType).Should().BeAssignableTo<System.Collections.IList>().Subject;
        foreach (var detection in detections)
        {
            typedDetections.Add(detection);
        }

        var tuple = method!.Invoke(_operator, new object?[] { typedDetections, iouThreshold });
        tuple.Should().NotBeNull();

        var tupleType = tuple!.GetType();
        var kept = ((System.Collections.IEnumerable)tupleType.GetField("Item1")!.GetValue(tuple)!)
            .Cast<object>()
            .ToList();
        var comparisons = (long)tupleType.GetField("Item2")!.GetValue(tuple)!;
        return (kept, comparisons);
    }

    private static string[] InvokeParseMetadataNames(string rawNames)
    {
        var resolverType = typeof(DeepLearningOperator).Assembly.GetType("Acme.Product.Infrastructure.Services.DeepLearningLabelResolver");
        resolverType.Should().NotBeNull();

        var method = resolverType!.GetMethod("ParseMetadataNames", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        return method!.Invoke(null, new object?[] { rawNames }).Should().BeAssignableTo<string[]>().Subject;
    }

    #endregion
}
