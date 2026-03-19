using System.Reflection;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class OperatorContractReconciliationTests
{
    [Fact]
    public void Metadata_ShouldExpose_Reconciled_Params_And_Ports()
    {
        var factory = new OperatorFactory();

        var deepLearning = factory.GetMetadata(OperatorType.DeepLearning)!;
        deepLearning.OutputPorts.Select(p => p.Name).Should().Contain("DetectionList");
        deepLearning.Parameters.Select(p => p.Name).Should().Contain(new[] { "UseGpu", "GpuDeviceId" });

        var sequenceJudge = factory.GetMetadata(OperatorType.DetectionSequenceJudge)!;
        sequenceJudge.InputPorts.Select(p => p.Name).Should().Contain("Detections");
        sequenceJudge.OutputPorts.Select(p => p.Name).Should().Contain(new[] { "IsMatch", "SortedDetections", "Message" });
        sequenceJudge.Parameters.Select(p => p.Name).Should().Contain(new[] { "ExpectedLabels", "SortBy", "Direction", "ExpectedCount", "MinConfidence" });

        var trigger = factory.GetMetadata(OperatorType.TriggerModule)!;
        trigger.InputPorts.Select(p => p.Name).Should().Contain("Signal");

        var typeConvert = factory.GetMetadata(OperatorType.TypeConvert)!;
        typeConvert.OutputPorts.Select(p => p.Name).Should().Contain(new[] { "AsString", "AsFloat", "AsInteger", "AsBoolean", "OriginalType" });

        var orb = factory.GetMetadata(OperatorType.OrbFeatureMatch)!;
        orb.Parameters.Select(p => p.Name).Should().Contain(new[] { "EnableSymmetryTest", "MinMatchCount" });
        orb.OutputPorts.Select(p => p.Name).Should().Contain("MatchPoint");

        var contour = factory.GetMetadata(OperatorType.ContourDetection)!;
        contour.Parameters.Select(p => p.Name).Should().Contain(new[] { "DrawContours", "MaxValue", "ThresholdType" });

        var acquisition = factory.GetMetadata(OperatorType.ImageAcquisition)!;
        acquisition.Parameters.Select(p => p.Name).Should().Contain(new[] { "SourceType", "FilePath", "CameraId", "ExposureTime", "Gain", "TriggerMode" });

        var blob = factory.GetMetadata(OperatorType.BlobAnalysis)!;
        blob.Parameters.Select(p => p.Name).Should().Contain(new[]
        {
            "MinCircularity",
            "MinConvexity",
            "MinInertiaRatio",
            "MinRectangularity",
            "MinEccentricity",
            "OutputDetailedFeatures"
        });
        blob.OutputPorts.Select(p => p.Name).Should().Contain("BlobFeatures");

        var caliper = factory.GetMetadata(OperatorType.CaliperTool)!;
        caliper.Parameters.Select(p => p.Name).Should().Contain(new[] { "MeasureMode", "PairDirection" });
        caliper.OutputPorts.Select(p => p.Name).Should().Contain(new[] { "PairDistances", "AverageDistance", "DistanceStdDev" });

        var shapeMatching = factory.GetMetadata(OperatorType.ShapeMatching)!;
        shapeMatching.DisplayName.Should().Be("旋转尺度模板匹配");

        var roiTransform = factory.GetMetadata(OperatorType.RoiTransform)!;
        roiTransform.InputPorts.Select(p => p.Name).Should().Contain(new[] { "BaseRoi", "Matches" });
        roiTransform.OutputPorts.Select(p => p.Name).Should().Contain("SearchRegion");

        // ArrayIndexer 契约检查 - 输入输出键名一致性
        var arrayIndexer = factory.GetMetadata(OperatorType.ArrayIndexer)!;
        arrayIndexer.InputPorts.Select(p => p.Name).Should().Contain("List");
        arrayIndexer.OutputPorts.Select(p => p.Name).Should().Contain(new[] { "Item", "Found", "Index" });
    }

    [Fact]
    public async Task TemplateMatch_Should_Support_Multiple_Matches_And_Center_Position()
    {
        var op = new Operator("template", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.8, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MaxMatches", 2, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Method", "CCoeffNormed", "string"));

        using var template = CreatePatternTemplate();
        using var scene = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(scene, template.MatReadOnly, 20, 30);
        CopyTemplate(scene, template.MatReadOnly, 100, 110);

        var inputs = new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        };

        var sut = new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>());
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("MatchCount");
        Convert.ToInt32(result.OutputData!["MatchCount"]).Should().Be(2);
        result.OutputData["Position"].Should().BeOfType<Position>();
    }

    [Fact]
    public async Task WidthMeasurement_Should_Apply_Custom_Direction_And_Expose_Image_Size_Separately()
    {
        var sut = new WidthMeasurementOperator(Substitute.For<ILogger<WidthMeasurementOperator>>());
        var op = new Operator("width", OperatorType.WidthMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureMode", "ManualLines", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NumSamples", 20, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Direction", "Custom", "string"));
        op.AddParameter(TestHelpers.CreateParameter("CustomAngle", 45.0, "double"));

        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(20, 20, 20, 120);
        inputs["Line2"] = new LineData(40, 20, 40, 120);

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("ImageWidth");
        Convert.ToDouble(result.OutputData!["Width"]).Should().BeGreaterThan(25.0);
    }

    [Fact]
    public async Task WidthMeasurement_Should_Use_SubpixelCaliper_When_ImageContainsEdges()
    {
        var sut = new WidthMeasurementOperator(Substitute.For<ILogger<WidthMeasurementOperator>>());
        var op = new Operator("width", OperatorType.WidthMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureMode", "ManualLines", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NumSamples", 24, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Direction", "Perpendicular", "string"));

        using var image = CreateEdgeWidthImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(48, 25, 48, 135);
        inputs["Line2"] = new LineData(102, 25, 102, 135);

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        Convert.ToDouble(result.OutputData!["Width"]).Should().BeApproximately(50, 4);
        Convert.ToInt32(result.OutputData["RefinedSampleCount"]).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Aggregator_Should_Expose_Stable_Keys_For_All_Modes()
    {
        var sut = new AggregatorOperator(Substitute.For<ILogger<AggregatorOperator>>());
        var op = new Operator("agg", OperatorType.Aggregator, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Average", "string"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Value1"] = 1,
            ["Value2"] = 2,
            ["Value3"] = 3
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Result"].Should().Be(2d);
        result.OutputData["MergedList"].Should().BeAssignableTo<IEnumerable<object>>();
        result.OutputData["MaxValue"].Should().Be(3d);
        result.OutputData["MinValue"].Should().Be(1d);
        result.OutputData["Average"].Should().Be(2d);
    }

    [Fact]
    public async Task ColorDetection_Should_Emit_ColorInfo()
    {
        var sut = new ColorDetectionOperator(Substitute.For<ILogger<ColorDetectionOperator>>());
        var op = new Operator("color", OperatorType.ColorDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("AnalysisMode", "Average", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ColorSpace", "HSV", "string"));

        using var image = TestHelpers.CreateTestImage(120, 80, new Scalar(0, 0, 255));
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("ColorInfo");
        var colorInfo = result.OutputData!["ColorInfo"].Should().BeOfType<Dictionary<string, object>>().Subject;
        colorInfo["AnalysisMode"].Should().Be("Average");
    }

    [Fact]
    public async Task ResultOutput_Should_Honor_Format_And_SaveToFile()
    {
        var sut = new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>());
        var op = new Operator("output", OperatorType.ResultOutput, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Format", "Text", "string"));
        op.AddParameter(TestHelpers.CreateParameter("SaveToFile", true, "bool"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Text"] = "OCR result text"
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Output"].Should().Be("OCR result text");
        result.OutputData.Should().ContainKey("FilePath");
        File.Exists(result.OutputData["FilePath"].ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task TriggerModule_Should_Use_Signal_Input_Port_In_External_Mode()
    {
        var sut = new TriggerModuleOperator(Substitute.For<ILogger<TriggerModuleOperator>>());
        var op = new Operator("trigger", OperatorType.TriggerModule, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("TriggerMode", "ExternalSignal", "string"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Signal"] = false });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Triggered"].Should().Be(false);
    }

    [Fact]
    public async Task ImageAcquisition_Should_Load_From_FilePath_Contract()
    {
        var cameraManager = Substitute.For<ICameraManager>();
        var sut = new ImageAcquisitionOperator(Substitute.For<ILogger<ImageAcquisitionOperator>>(), cameraManager);
        var op = new Operator("acq", OperatorType.ImageAcquisition, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("SourceType", "File", "string"));

        using var image = CreatePatternTemplate();
        var tempPath = Path.Combine(Path.GetTempPath(), $"acq_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempPath, image.GetBytes());

        try
        {
            op.AddParameter(TestHelpers.CreateParameter("FilePath", tempPath, "string"));
            var result = await sut.ExecuteAsync(op, null);

            result.IsSuccess.Should().BeTrue();
            result.OutputData.Should().ContainKey("Image");
            result.OutputData.Should().ContainKey("Width");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task CircleMeasurement_Should_Emit_Center_And_Circle()
    {
        var sut = new CircleMeasurementOperator(Substitute.For<ILogger<CircleMeasurementOperator>>());
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinRadius", 20, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MaxRadius", 40, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Param1", 100.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Param2", 20.0, "double"));

        using var image = CreateCircleImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Center");
        result.OutputData.Should().ContainKey("Circle");
        result.OutputData!["Center"].Should().BeOfType<Position>();
    }

    [Fact]
    public async Task TypeConvert_Should_Reject_Legacy_Value_Key_When_Input_Is_Missing()
    {
        var sut = new TypeConvertOperator(Substitute.For<ILogger<TypeConvertOperator>>());
        var op = new Operator("convert", OperatorType.TypeConvert, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Value"] = 42 });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShapeMatching_Should_Find_Scaled_Template()
    {
        var sut = new ShapeMatchingOperator(Substitute.For<ILogger<ShapeMatchingOperator>>());
        var op = new Operator("shape", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", 0.6, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", 0.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", 0.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMin", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMax", 1.6, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleStep", 0.25, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", 3, "int"));

        using var template = CreatePatternTemplate();
        using var scaledTemplate = new Mat();
        Cv2.Resize(template.MatReadOnly, scaledTemplate, new Size(), 1.5, 1.5, InterpolationFlags.Linear);

        using var scene = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(scene, scaledTemplate, 80, 70);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        Convert.ToInt32(result.OutputData!["MatchCount"]).Should().BeGreaterThan(0);
        var matches = result.OutputData["Matches"].Should().BeAssignableTo<IEnumerable<object>>().Subject.Cast<Dictionary<string, object>>().ToList();
        matches.Should().NotBeEmpty();
        Convert.ToDouble(matches[0]["Scale"]).Should().BeGreaterThan(1.2);
    }

    [Fact]
    public async Task GradientShapeMatch_Should_Limit_Cache_Size_To_Eight()
    {
        var sut = new GradientShapeMatchOperator(Substitute.For<ILogger<GradientShapeMatchOperator>>());

        for (var i = 0; i < 10; i++)
        {
            using var template = CreateVariantTemplate(i);
            using var scene = template.MatReadOnly.Clone();
            var op = new Operator($"grad_{i}", OperatorType.GradientShapeMatch, 0, 0);
            op.AddParameter(TestHelpers.CreateParameter("EnableCache", true, "bool"));
            op.AddParameter(TestHelpers.CreateParameter("MinScore", 30.0, "double"));

            var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = new ImageWrapper(scene),
                ["Template"] = template
            });

            result.IsSuccess.Should().BeTrue();
        }

        var cacheField = typeof(GradientShapeMatchOperator).GetField("_matcherCache", BindingFlags.Instance | BindingFlags.NonPublic);
        cacheField.Should().NotBeNull();
        var cache = cacheField!.GetValue(sut).Should().BeAssignableTo<System.Collections.IDictionary>().Subject;
        cache.Count.Should().BeLessOrEqualTo(8);
    }

    [Fact]
    public async Task ArrayIndexer_Should_Use_List_Input_And_Item_Output()
    {
        // 测试 ArrayIndexer 输入输出契约一致性
        var sut = new ArrayIndexerOperator(Substitute.For<ILogger<ArrayIndexerOperator>>());
        var op = new Operator("array", OperatorType.ArrayIndexer, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Index", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Index", 1, "int"));

        var items = new List<DetectionResult>
        {
            new("First", 0.5f, 0, 0, 10, 10),
            new("Second", 0.9f, 10, 0, 20, 20),
            new("Third", 0.7f, 20, 0, 30, 30)
        };

        // 使用 "List" 输入键（契约声明的端口名）
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["List"] = items
        });

        result.IsSuccess.Should().BeTrue();
        // 验证输出键与声明的输出端口一致
        result.OutputData.Should().ContainKey("Item");
        result.OutputData.Should().ContainKey("Found");
        result.OutputData.Should().ContainKey("Index");
        result.OutputData!["Found"].Should().Be(true);
        result.OutputData["Index"].Should().Be(1);
    }

    [Fact]
    public async Task ArrayIndexer_BackwardCompatibility_Should_Accept_Items_Key()
    {
        // 测试 ArrayIndexer 向后兼容旧的 "Items" 输入键
        var sut = new ArrayIndexerOperator(Substitute.For<ILogger<ArrayIndexerOperator>>());
        var op = new Operator("array", OperatorType.ArrayIndexer, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "First", "string"));

        var items = new List<DetectionResult>
        {
            new("Legacy", 0.8f, 0, 0, 10, 10)
        };

        // 使用旧的 "Items" 输入键
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Items"] = items
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Item");
        result.OutputData!["Found"].Should().Be(true);
    }

    private static ImageWrapper CreatePatternTemplate()
    {
        var mat = new Mat(32, 32, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 4, 24, 24), Scalar.White, -1);
        Cv2.Line(mat, new Point(4, 16), new Point(28, 16), Scalar.Black, 2);
        Cv2.Circle(mat, new Point(16, 10), 4, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateVariantTemplate(int seed)
    {
        var mat = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4 + (seed % 4), 4, 24, 24), Scalar.White, -1);
        Cv2.Circle(mat, new Point(10 + seed, 28 - (seed % 6)), 4, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateEdgeWidthImage()
    {
        var mat = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 20, 50, 120), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateCircleImage()
    {
        var mat = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(80, 80), 28, Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    private static void CopyTemplate(Mat scene, Mat template, int x, int y)
    {
        using var roi = new Mat(scene, new Rect(x, y, template.Width, template.Height));
        template.CopyTo(roi);
    }
}
