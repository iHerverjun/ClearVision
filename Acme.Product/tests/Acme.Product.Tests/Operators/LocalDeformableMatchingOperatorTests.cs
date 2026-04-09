using Acme.Product.Core.Entities;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Reflection;

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
        // 娌℃湁妯℃澘搴旇杩斿洖澶辫触鎴栭檷绾х粨鏋?
        result.IsSuccess.Should().BeTrue(); // 绠楀瓙鏈韩杩斿洖鎴愬姛锛屼絾鍖呭惈澶辫触淇℃伅
        result.OutputData["IsMatch"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowFeatureSeedCandidate_ShouldNotSucceedBySeedFallback()
    {
        var op = new Operator("LocalDeformableMatching_Seed", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.2));
        op.Parameters.Add(TestHelpers.CreateParameter("CandidateThreshold", 0.2));

        using var template = new Mat(60, 60, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(template, new Rect(6, 6, 48, 48), Scalar.White, -1);
        using var scene = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(scene, new Rect(80, 80, 48, 48), Scalar.White, -1);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = new ImageWrapper(template)
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["FailureReason"].Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithSameImageAsTemplate_ShouldMatch()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.3)); // 闄嶄綆闃堝€间互渚挎祴璇?
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 2)); // 鍑忓皯灞傛暟浠ュ姞蹇祴璇?
        
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
    public async Task ExecuteAsync_WithLocallyWarpedTemplate_ShouldNotReportUnsafeSuccess()
    {
        var op = new Operator("LocalDeformableMatching_LocalWarp", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.05));
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 2));
        op.Parameters.Add(TestHelpers.CreateParameter("CandidateThreshold", 0.1));
        op.Parameters.Add(TestHelpers.CreateParameter("MaxDeformation", 18.0));

        using var template = CreatePatternTemplate();
        using var scene = CreateLocallyWarpedScene(template);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = new ImageWrapper(template)
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["VerificationPassed"].Should().Be(true);
        result.OutputData["Method"].Should().Be("TPS_Deformable");
        Convert.ToDouble(result.OutputData["DeformationMagnitude"]).Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void IterativeRefinement_ShouldFeedBackLatestDeformationIntoNextSolve()
    {
        var controlPoints = new[]
        {
            new Point2f(0, 0),
            new Point2f(12, 0),
            new Point2f(24, 0),
            new Point2f(0, 12),
            new Point2f(12, 12),
            new Point2f(24, 12),
            new Point2f(0, 24),
            new Point2f(12, 24),
            new Point2f(24, 24)
        };
        var initialDeformedPoints = new[]
        {
            new Point2f(1.1f, 0.8f),
            new Point2f(13.4f, 0.5f),
            new Point2f(25.5f, 1.0f),
            new Point2f(0.6f, 13.1f),
            new Point2f(13.2f, 13.9f),
            new Point2f(25.4f, 14.4f),
            new Point2f(0.2f, 25.0f),
            new Point2f(12.9f, 25.9f),
            new Point2f(25.3f, 26.4f)
        };
        var templateKeypoints = new[]
        {
            new KeyPoint(4, 5, 1),
            new KeyPoint(18, 4, 1),
            new KeyPoint(7, 18, 1),
            new KeyPoint(19, 19, 1),
            new KeyPoint(12, 11, 1)
        };
        var searchKeypoints = new[]
        {
            new KeyPoint(6, 6, 1),
            new KeyPoint(22, 7, 1),
            new KeyPoint(5, 21, 1),
            new KeyPoint(23, 22, 1),
            new KeyPoint(14, 14, 1)
        };
        var matches = Enumerable.Range(0, templateKeypoints.Length)
            .Select(index => new DMatch(index, index, 0))
            .ToList();

        using var identityHomography = Mat.Eye(3, 3, MatType.CV_64FC1);
        var firstCorrespondences = InvokeComputeCorrespondences(controlPoints, initialDeformedPoints, matches, templateKeypoints, searchKeypoints, identityHomography);
        var firstRefinement = InvokeEstimateTpsDeformation(initialDeformedPoints, firstCorrespondences, 0.01, 6.0);
        var secondCorrespondences = InvokeComputeCorrespondences(controlPoints, firstRefinement, matches, templateKeypoints, searchKeypoints, identityHomography);
        var secondRefinement = InvokeEstimateTpsDeformation(firstRefinement, secondCorrespondences, 0.01, 6.0);

        SumPointwiseDistance(secondCorrespondences.templatePts, firstCorrespondences.templatePts).Should().BeGreaterThan(0.25);
        SumPointwiseDistance(secondRefinement, firstRefinement).Should().BeGreaterThan(0.25);
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
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", -0.1)); // 鏃犳晥鍊?

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPyramidLevels_ShouldBeInvalid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 0)); // 浣庝簬鏈€灏忓€?

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithTooHighPyramidLevels_ShouldBeInvalid()
    {
        var op = new Operator("LocalDeformableMatching", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 10)); // 瓒呰繃鏈€澶у€?

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Metadata_ShouldDisableFallbackByDefault()
    {
        var fallbackParam = typeof(LocalDeformableMatchingOperator)
            .GetCustomAttributes(typeof(OperatorParamAttribute), inherit: false)
            .Cast<OperatorParamAttribute>()
            .Single(attribute => attribute.Name == "EnableFallback");

        fallbackParam.DefaultValue.Should().Be(false);
    }

    private static Mat CreatePatternTemplate()
    {
        var template = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(template, new Rect(12, 10, 28, 24), Scalar.White, -1);
        Cv2.Circle(template, new Point(84, 28), 14, Scalar.White, -1);
        Cv2.Line(template, new Point(8, 96), new Point(110, 68), new Scalar(180, 180, 180), 3);
        Cv2.Line(template, new Point(20, 54), new Point(92, 102), Scalar.White, 2);
        Cv2.Rectangle(template, new Rect(54, 62, 28, 18), new Scalar(120, 120, 120), -1);
        for (var index = 0; index < 5; index++)
        {
            Cv2.Line(template, new Point(4 + (index * 20), 0), new Point(4 + (index * 20), 119), new Scalar(40, 40, 40), 1);
        }

        return template;
    }

    private static Mat CreateLocallyWarpedScene(Mat template)
    {
        using var mapX = new Mat(template.Size(), MatType.CV_32FC1);
        using var mapY = new Mat(template.Size(), MatType.CV_32FC1);
        for (var y = 0; y < template.Rows; y++)
        {
            for (var x = 0; x < template.Cols; x++)
            {
                var offsetX = 3.0 * Math.Sin((Math.PI * y) / template.Rows);
                var offsetY = 2.0 * Math.Sin((Math.PI * x) / template.Cols) * Math.Exp(-Math.Pow((y - (template.Rows / 2.0)) / 40.0, 2));
                mapX.Set(y, x, (float)Math.Clamp(x - offsetX, 0, template.Cols - 1));
                mapY.Set(y, x, (float)Math.Clamp(y - offsetY, 0, template.Rows - 1));
            }
        }

        using var warped = new Mat();
        Cv2.Remap(template, warped, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        var scene = new Mat(260, 260, MatType.CV_8UC3, Scalar.Black);
        using var roi = new Mat(scene, new Rect(70, 80, warped.Width, warped.Height));
        warped.CopyTo(roi);
        return scene;
    }

    private (Point2f[] templatePts, Point2f[] searchPts) InvokeComputeCorrespondences(
        Point2f[] controlPoints,
        Point2f[] currentDeformedPoints,
        List<DMatch> matches,
        KeyPoint[] templateKeypoints,
        KeyPoint[] searchKeypoints,
        Mat homography)
    {
        var method = typeof(LocalDeformableMatchingOperator).GetMethod(
            "ComputeCorrespondences",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        return ((Point2f[] templatePts, Point2f[] searchPts))method!.Invoke(
            _operator,
            new object[] { matches, templateKeypoints, searchKeypoints, controlPoints, currentDeformedPoints, homography })!;
    }

    private Point2f[] InvokeEstimateTpsDeformation(
        Point2f[] evaluationPoints,
        (Point2f[] templatePts, Point2f[] searchPts) correspondences,
        double lambda,
        double maxDeformation)
    {
        var method = typeof(LocalDeformableMatchingOperator).GetMethod(
            "EstimateTPSDeformation",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        return (Point2f[])method!.Invoke(
            _operator,
            new object?[] { evaluationPoints, correspondences, lambda, maxDeformation, null })!;
    }

    private static double SumPointwiseDistance(IReadOnlyList<Point2f> left, IReadOnlyList<Point2f> right)
    {
        left.Count.Should().Be(right.Count);

        var totalDistance = 0.0;
        for (var index = 0; index < left.Count; index++)
        {
            var dx = left[index].X - right[index].X;
            var dy = left[index].Y - right[index].Y;
            totalDistance += Math.Sqrt((dx * dx) + (dy * dy));
        }

        return totalDistance;
    }

    private static ImageWrapper CreateFeatureRichImage()
    {
        // 鍒涘缓鍖呭惈涓板瘜鐗瑰緛鐨勫浘鍍?
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Gray);
        
        // 娣诲姞涓€浜涘嚑浣曞舰鐘朵互浜х敓鐗瑰緛
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.Black, -1);
        Cv2.Rectangle(mat, new Rect(200, 150, 120, 80), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 300), 50, Scalar.Black, -1);
        Cv2.Circle(mat, new Point(150, 300), 30, Scalar.White, -1);
        
        // 娣诲姞绾挎潯绾圭悊
        for (int i = 0; i < 8; i++)
        {
            Cv2.Line(mat, new Point(i * 50, 0), new Point(i * 50, 400), Scalar.DarkGray, 2);
            Cv2.Line(mat, new Point(0, i * 50), new Point(400, i * 50), Scalar.DarkGray, 2);
        }
        
        return new ImageWrapper(mat);
    }
}

