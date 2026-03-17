using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class Stage1_W4_ShapeMatchVerificationTests
{
    private readonly ShapeMatchingOperator _sut;

    public Stage1_W4_ShapeMatchVerificationTests()
    {
        _sut = new ShapeMatchingOperator(Substitute.For<ILogger<ShapeMatchingOperator>>());
    }

    [Fact]
    public async Task W4_Verification_ShapeMatching_Precision_ShouldBeBelow_0_1px()
    {
        // Standard shape template.
        using var template = CreateStandardTemplate(size: 64);

        // Subpixel placement.
        const double expectedX = 173.35;
        const double expectedY = 121.60;
        using var scene = CreateSceneByWarpingTemplate(template.MatReadOnly, width: 512, height: 512, expectedX, expectedY);

        var op = CreateOperator(minScore: 0.4, maxMatches: 1, numLevels: 1);
        var inputs = new Dictionary<string, object>
        {
            ["Image"] = scene,
            ["Template"] = template
        };

        var result = await _sut.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();

        var matches = Assert.IsType<List<Dictionary<string, object>>>(result.OutputData!["Matches"]);
        matches.Count.Should().BeGreaterThan(0);

        var m = matches[0];
        var xSub = Convert.ToDouble(m["XSubpixel"]);
        var ySub = Convert.ToDouble(m["YSubpixel"]);

        // Document requirement: <0.1px.
        Math.Abs(xSub - expectedX).Should().BeLessThan(0.1);
        Math.Abs(ySub - expectedY).Should().BeLessThan(0.1);
    }

    [Fact]
    public async Task W4_Verification_ShapeMatching_Occlusion30Percent_ShouldKeepScoreAbove_0_6()
    {
        using var template = CreateStandardTemplate(size: 64);

        const double expectedX = 200.0;
        const double expectedY = 160.0;
        using var baseScene = CreateSceneByWarpingTemplate(template.MatReadOnly, width: 512, height: 512, expectedX, expectedY);

        // Occlude 30% of the template area (right-side stripe).
        var occludedMat = baseScene.MatReadOnly.Clone();
        var occX = (int)Math.Round(expectedX + (template.Width * 0.70));
        var occW = (int)Math.Round(template.Width * 0.30);
        var occRect = new Rect(occX, (int)Math.Round(expectedY), occW, template.Height);
        Cv2.Rectangle(occludedMat, occRect, Scalar.Black, thickness: -1);
        using var scene = new ImageWrapper(occludedMat);

        var op = CreateOperator(minScore: 0.6, maxMatches: 1, numLevels: 2);
        var inputs = new Dictionary<string, object>
        {
            ["Image"] = scene,
            ["Template"] = template
        };

        var result = await _sut.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();

        var matches = Assert.IsType<List<Dictionary<string, object>>>(result.OutputData!["Matches"]);
        matches.Count.Should().BeGreaterThan(0);

        var score = Convert.ToDouble(matches[0]["Score"]);
        score.Should().BeGreaterThanOrEqualTo(0.6);
    }

    [Fact]
    public async Task W4_Verification_ShapeMatching_IlluminationPlusMinus50Percent_ScoreChangeShouldBeBelow_0_1()
    {
        using var template = CreateStandardTemplate(size: 64);

        const double expectedX = 180.25;
        const double expectedY = 140.75;
        using var baseScene = CreateSceneByWarpingTemplate(template.MatReadOnly, width: 512, height: 512, expectedX, expectedY);

        using var brighter = AdjustBrightness(baseScene.MatReadOnly, alpha: 1.5);
        using var darker = AdjustBrightness(baseScene.MatReadOnly, alpha: 0.5);

        var op = CreateOperator(minScore: 0.4, maxMatches: 1, numLevels: 2);

        var baseScore = await RunAndGetPrimaryScore(op, baseScene, template);
        var brightScore = await RunAndGetPrimaryScore(op, brighter, template);
        var darkScore = await RunAndGetPrimaryScore(op, darker, template);

        Math.Abs(brightScore - baseScore).Should().BeLessThan(0.1);
        Math.Abs(darkScore - baseScore).Should().BeLessThan(0.1);
    }

    private async Task<double> RunAndGetPrimaryScore(Operator op, ImageWrapper scene, ImageWrapper template)
    {
        var inputs = new Dictionary<string, object>
        {
            // OperatorBase will Release() the input wrappers after execution. AddRef() keeps our test-owned wrappers alive.
            ["Image"] = scene.AddRef(),
            ["Template"] = template.AddRef()
        };

        var result = await _sut.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        var matches = Assert.IsType<List<Dictionary<string, object>>>(result.OutputData!["Matches"]);
        matches.Count.Should().BeGreaterThan(0);
        return Convert.ToDouble(matches[0]["Score"]);
    }

    private static Operator CreateOperator(double minScore, int maxMatches, int numLevels)
    {
        var op = new Operator("ShapeMatch", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", minScore, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MaxMatches", maxMatches, "int"));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", 0.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", 0.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMin", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMax", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleStep", 0.1, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", numLevels, "int"));
        return op;
    }

    private static ImageWrapper CreateStandardTemplate(int size)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(8, 10, size - 16, size - 20), Scalar.White, thickness: -1);
        Cv2.Circle(mat, new Point(size / 2, size / 2), size / 6, Scalar.Black, thickness: -1);
        Cv2.Line(mat, new Point(12, size - 14), new Point(size - 14, 12), Scalar.White, thickness: 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSceneByWarpingTemplate(Mat template, int width, int height, double x, double y)
    {
        using var transform = new Mat(2, 3, MatType.CV_64FC1);
        transform.Set(0, 0, 1.0); transform.Set(0, 1, 0.0); transform.Set(0, 2, x);
        transform.Set(1, 0, 0.0); transform.Set(1, 1, 1.0); transform.Set(1, 2, y);

        var scene = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.WarpAffine(template, scene, transform, new Size(width, height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return new ImageWrapper(scene);
    }

    private static ImageWrapper AdjustBrightness(Mat src, double alpha)
    {
        var dst = new Mat();
        src.ConvertTo(dst, MatType.CV_8UC1, alpha, 0);
        return new ImageWrapper(dst);
    }
}
