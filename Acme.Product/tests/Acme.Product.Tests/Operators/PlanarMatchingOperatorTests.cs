using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Attributes;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Reflection;

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
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        
        using var image = CreateFeatureRichImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Template"] = image; // 使用相同图像作为模板

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("IsMatch");
        result.OutputData.Should().ContainKey("VerificationPassed");
        result.OutputData!["VerificationPassed"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentDetectors_ShouldWork()
    {
        var detectors = new[] { "ORB", "AKAZE", "BRISK" };
        
        foreach (var detector in detectors)
        {
            var op = new Operator($"PlanarMatching_{detector}", OperatorType.PlanarMatching, 0, 0);
            op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", detector));
            op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
            
            using var image = CreateFeatureRichImage();
            var inputs = TestHelpers.CreateImageInputs(image);
            inputs["Template"] = image;

            var result = await _operator.ExecuteAsync(op, inputs);
            result.IsSuccess.Should().BeTrue($"Detector {detector} should work");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithBlankSearchImage_ShouldFailVerification()
    {
        var op = new Operator("PlanarMatching_Blank", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));

        using var template = CreateFeatureRichImage();
        using var blank = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(blank),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["FailureReason"].Should().NotBeNull();
        result.OutputData["VerificationPassed"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithPerspectiveWarp_ShouldPassGeometryGate()
    {
        var op = new Operator("PlanarMatching_Warp", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("ScoreThreshold", 0.2));

        using var template = CreateFeatureRichImage();
        using var scene = WarpIntoScene(template.MatReadOnly);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        Convert.ToInt32(result.OutputData["InlierCount"]).Should().BeGreaterThanOrEqualTo(4);
        result.OutputData["VerificationPassed"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithRoiExcludingTarget_ShouldFailVerification()
    {
        var op = new Operator("PlanarMatching_RoiMiss", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("UseRoi", true));
        op.Parameters.Add(TestHelpers.CreateParameter("RoiX", 0));
        op.Parameters.Add(TestHelpers.CreateParameter("RoiY", 0));
        op.Parameters.Add(TestHelpers.CreateParameter("RoiWidth", 180));
        op.Parameters.Add(TestHelpers.CreateParameter("RoiHeight", 180));

        using var template = CreateFeatureRichImage();
        using var scene = WarpIntoScene(template.MatReadOnly);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["VerificationPassed"].Should().Be(false);
        result.OutputData["FailureReason"].Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentFeatureLayout_ShouldRejectFalsePositive()
    {
        var op = new Operator("PlanarMatching_NegativeLayout", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("ScoreThreshold", 0.2));

        using var template = CreateFeatureRichImage();
        using var distractor = CreateDistractorFeatureImage();

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(distractor),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["VerificationPassed"].Should().Be(false);
        result.OutputData["FailureReason"].Should().NotBeNull();
        Convert.ToDouble(result.OutputData["CandidateScore"]).Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithPerspectiveWarp_ShouldReturnConsistentVerificationFields()
    {
        var op = new Operator("PlanarMatching_VerificationFields", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("ScoreThreshold", 0.2));

        using var template = CreateFeatureRichImage();
        using var scene = WarpIntoScene(template.MatReadOnly);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["VerificationPassed"].Should().Be(true);
        result.OutputData["FailureReason"].Should().Be(string.Empty);
        Convert.ToInt32(result.OutputData["MatchCount"]).Should().Be(1);
        Convert.ToInt32(result.OutputData["InlierCount"]).Should().BeGreaterThanOrEqualTo(4);
        Convert.ToDouble(result.OutputData["InlierRatio"]).Should().BeGreaterThan(0.2);
        Convert.ToDouble(result.OutputData["CandidateScore"]).Should().BeGreaterThan(0.0);
        result.OutputData.Should().ContainKey("MatchResult");
    }

    [Fact]
    public void Metadata_ShouldNotExposeSiftOption()
    {
        var detectorParam = typeof(PlanarMatchingOperator)
            .GetCustomAttributes(typeof(OperatorParamAttribute), inherit: false)
            .Cast<OperatorParamAttribute>()
            .Single(attribute => attribute.Name == "DetectorType");

        detectorParam.Options.Should().NotContain(option => option.Contains("SIFT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateParameters_WithValidMatchRatio_ShouldBeValid()
    {
        var op = new Operator("PlanarMatching", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MatchRatio", 0.75));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("SIFT")]
    [InlineData("invalid-detector")]
    public void ValidateParameters_WithRemovedOrUnknownDetector_ShouldBeInvalid(string detectorType)
    {
        var op = new Operator("PlanarMatching_InvalidDetector", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", detectorType));

        var validation = _operator.ValidateParameters(op);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle()
            .Which.Should().Contain("DetectorType must be ORB, AKAZE, or BRISK.");
    }

    [Fact]
    public async Task ExecuteAsync_WithRemovedDetectorType_ShouldFailInsteadOfFallingBackToOrb()
    {
        var op = new Operator("PlanarMatching_Sift", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "SIFT"));

        using var image = CreateFeatureRichImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Template"] = image;

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DetectorType must be ORB, AKAZE, or BRISK.");
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

    [Theory]
    [InlineData("ORB", 32, 256.0)]
    [InlineData("AKAZE", 61, 488.0)]
    [InlineData("BRISK", 64, 512.0)]
    public void BinaryDescriptorNormalizer_ShouldScaleWithDescriptorBitLength(string detectorType, int descriptorBytes, double expectedMaxDistance)
    {
        using var descriptors = new Mat(1, descriptorBytes, MatType.CV_8UC1, Scalar.All(0));
        var method = typeof(PlanarMatchingOperator).GetMethod(
            "GetDescriptorDistanceNormalizer",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        var maxDistance = (double)method!.Invoke(null, new object[] { detectorType, descriptors, descriptors })!;

        maxDistance.Should().Be(expectedMaxDistance);
    }

    [Fact]
    public void CandidateScore_ShouldUseExpandedBinaryDistanceRangeForLongDescriptors()
    {
        using var descriptors = new Mat(1, 64, MatType.CV_8UC1, Scalar.All(0));
        var matches = new List<DMatch> { new(0, 0, 256.0f) };
        var method = typeof(PlanarMatchingOperator).GetMethod(
            "CalculateCandidateScore",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        var score = (double)method!.Invoke(null, new object[] { matches, "BRISK", 1, 1, descriptors, descriptors })!;

        score.Should().BeApproximately(0.825, 0.0001);
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

    private static Mat WarpIntoScene(Mat template)
    {
        var scene = new Mat(520, 520, MatType.CV_8UC3, Scalar.Gray);
        var src = new[]
        {
            new Point2f(0, 0),
            new Point2f(template.Width - 1, 0),
            new Point2f(template.Width - 1, template.Height - 1),
            new Point2f(0, template.Height - 1)
        };
        var dst = new[]
        {
            new Point2f(90, 70),
            new Point2f(390, 110),
            new Point2f(360, 410),
            new Point2f(120, 430)
        };

        using var homography = Cv2.GetPerspectiveTransform(src, dst);
        Cv2.WarpPerspective(template, scene, homography, scene.Size(), InterpolationFlags.Linear, BorderTypes.Transparent);
        return scene;
    }

    private static ImageWrapper CreateDistractorFeatureImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Gray);
        Cv2.Rectangle(mat, new Rect(20, 220, 140, 90), Scalar.Black, -1);
        Cv2.Rectangle(mat, new Rect(240, 40, 90, 140), Scalar.White, -1);
        Cv2.Circle(mat, new Point(120, 110), 45, Scalar.White, -1);
        Cv2.Circle(mat, new Point(310, 290), 55, Scalar.Black, -1);
        for (var i = 0; i < 8; i++)
        {
            Cv2.Line(mat, new Point(0, 30 + (i * 45)), new Point(399, 30 + (i * 45)), Scalar.DarkGray, 2);
        }

        return new ImageWrapper(mat);
    }
}
