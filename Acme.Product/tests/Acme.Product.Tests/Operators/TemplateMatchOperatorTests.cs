// TemplateMatchOperatorTests.cs
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class TemplateMatchOperatorTests
{
    private readonly TemplateMatchOperator _operator;

    public TemplateMatchOperatorTests()
    {
        _operator = new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTemplateMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.TemplateMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplate_ShouldKeepOutputImageUsable()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);

        using var src = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(src, new Rect(30, 30, 40, 40), Scalar.White, -1);

        using var templateRoi = new Mat(src, new Rect(30, 30, 40, 40));
        using var template = templateRoi.Clone();

        var inputs = new Dictionary<string, object>
        {
            { "Image", src.ToBytes(".png") },
            { "Template", template.ToBytes(".png") }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Image");

        var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        var outputBytes = outputImage.GetBytes();
        outputBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMismatchedTemplate_ShouldExposeFailureReason()
    {
        var op = new Operator("test", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.95, "double"));

        using var src = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(src, new Point(60, 60), 18, Scalar.White, -1);

        using var template = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(template, new Rect(4, 4, 32, 32), Scalar.White, -1);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["FailureReason"].Should().Be("No match above threshold.");
        result.OutputData["Method"].Should().Be("CCoeffNormed");
    }

    [Fact]
    public async Task ExecuteAsync_WithRoiExcludingTarget_ShouldReturnNoMatch()
    {
        var op = new Operator("template_roi", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("UseRoi", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("RoiX", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiY", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiWidth", 60, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiHeight", 60, "int"));

        using var src = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        CopyTemplate(src, template, 110, 110);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithMask_ShouldRestrictAllowedMatchRegion()
    {
        var op = new Operator("template_mask", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MaxMatches", 1, "int"));

        using var src = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        using var mask = new Mat(180, 180, MatType.CV_8UC1, Scalar.Black);

        CopyTemplate(src, template, 20, 20);
        CopyTemplate(src, template, 100, 110);
        Cv2.Rectangle(mask, new Rect(95, 105, 50, 50), Scalar.White, -1);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png"),
            ["Mask"] = mask.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        var position = result.OutputData["Position"].Should().BeOfType<Position>().Subject;
        position.X.Should().BeGreaterThan(110);
        position.Y.Should().BeGreaterThan(120);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaskExcludingAllCandidates_ShouldReturnFailureContract()
    {
        var op = new Operator("template_mask_blocked", OperatorType.TemplateMatching, 0, 0);

        using var src = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        using var mask = new Mat(180, 180, MatType.CV_8UC1, Scalar.Black);

        CopyTemplate(src, template, 80, 70);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png"),
            ["Mask"] = mask.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["MatchCount"].Should().Be(0);
        result.OutputData["FailureReason"].Should().Be("No match above threshold.");
    }

    [Fact]
    public async Task ExecuteAsync_WithRoiAndNonOverlappingMask_ShouldTreatSearchAreaAsBlocked()
    {
        var op = new Operator("template_roi_mask_disjoint", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("UseRoi", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("RoiX", 90, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiY", 80, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiWidth", 80, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiHeight", 80, "int"));

        using var src = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        using var mask = new Mat(40, 40, MatType.CV_8UC1, Scalar.White);

        CopyTemplate(src, template, 108, 96);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png"),
            ["Mask"] = mask.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["MatchCount"].Should().Be(0);
        result.OutputData["FailureReason"].Should().Be("No match above threshold.");
    }

    [Fact]
    public async Task ExecuteAsync_WithLowContrastFilledTemplate_ShouldStillMatch()
    {
        var op = new Operator("template_low_contrast_pad", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.95, "double"));

        using var src = new Mat(180, 180, MatType.CV_8UC3, new Scalar(120, 120, 120));
        using var template = CreateLowContrastPadTemplate();
        CopyTemplate(src, template, 68, 74);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["FailureReason"].Should().Be(string.Empty);
        var position = result.OutputData["Position"].Should().BeOfType<Position>().Subject;
        position.X.Should().BeApproximately(86, 1.0);
        position.Y.Should().BeApproximately(92, 1.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSeparatedTargets_ShouldReturnDistinctMatches()
    {
        var op = new Operator("template_multi", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MaxMatches", 2, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.75, "double"));

        using var src = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        CopyTemplate(src, template, 20, 25);
        CopyTemplate(src, template, 150, 135);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["MatchCount"].Should().Be(2);

        var matches = result.OutputData["Matches"]
            .Should().BeAssignableTo<IEnumerable<object>>()
            .Subject
            .Cast<Dictionary<string, object>>()
            .Select(match => match["Position"].Should().BeOfType<Position>().Subject)
            .OrderBy(position => position.X)
            .ToList();

        matches.Should().HaveCount(2);
        matches[0].X.Should().BeApproximately(36, 1.0);
        matches[0].Y.Should().BeApproximately(41, 1.0);
        matches[1].X.Should().BeApproximately(166, 1.0);
        matches[1].Y.Should().BeApproximately(151, 1.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithBroadPrimaryPeak_ShouldKeepRoomForSecondaryTarget()
    {
        var op = new Operator("template_plateau_multi", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "CCorrNormed", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.78, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MaxMatches", 2, "int"));

        using var src = new Mat(260, 260, MatType.CV_8UC3, new Scalar(40, 40, 40));
        using var template = CreateBroadPeakTemplate();
        using var weakerTarget = template.Clone();
        Cv2.Rectangle(weakerTarget, new Rect(44, 20, 12, 18), new Scalar(70, 70, 70), -1);

        CopyTemplate(src, template, 22, 28);
        CopyTemplate(src, weakerTarget, 154, 148);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["MatchCount"].Should().Be(2);

        var matches = result.OutputData["Matches"]
            .Should().BeAssignableTo<IEnumerable<object>>()
            .Subject
            .Cast<Dictionary<string, object>>()
            .Select(match => match["Position"].Should().BeOfType<Position>().Subject)
            .OrderBy(position => position.X)
            .ToList();

        matches.Should().HaveCount(2);
        matches[0].X.Should().BeApproximately(58, 2.0);
        matches[0].Y.Should().BeApproximately(64, 2.0);
        matches[1].X.Should().BeApproximately(190, 2.0);
        matches[1].Y.Should().BeApproximately(184, 2.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithGradientDomainAndRoiMask_ShouldUseGlobalCoordinates()
    {
        var op = new Operator("template_gradient_roi_mask", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Domain", "Gradient", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.45, "double"));
        op.AddParameter(TestHelpers.CreateParameter("UseRoi", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("RoiX", 60, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiY", 50, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiWidth", 120, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiHeight", 120, "int"));

        using var src = new Mat(220, 220, MatType.CV_8UC3, new Scalar(20, 20, 20));
        using var template = CreatePatternTemplate();
        using var shifted = template.Clone();
        using var mask = new Mat(220, 220, MatType.CV_8UC1, Scalar.Black);

        shifted.ConvertTo(shifted, shifted.Type(), 1.2, 40);
        CopyTemplate(src, shifted, 96, 88);
        Cv2.Rectangle(mask, new Rect(90, 82, 50, 50), Scalar.White, -1);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png"),
            ["Mask"] = mask.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["Method"].Should().Be("CCoeffNormed:Gradient");
        var position = result.OutputData["Position"].Should().BeOfType<Position>().Subject;
        position.X.Should().BeApproximately(112, 1.5);
        position.Y.Should().BeApproximately(104, 1.5);
    }

    [Fact]
    public async Task ExecuteAsync_WithEdgeDomain_ShouldHandleBrightnessShift()
    {
        var op = new Operator("template_edge", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Domain", "Edge", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.55, "double"));

        using var template = CreatePatternTemplate();
        using var src = new Mat(180, 180, MatType.CV_8UC3, new Scalar(30, 30, 30));
        using var shifted = template.Clone();
        shifted.ConvertTo(shifted, shifted.Type(), 0.5, 120);
        CopyTemplate(src, shifted, 70, 60);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["Method"].Should().Be("CCoeffNormed:Edge");
    }

    [Theory]
    [InlineData("SqDiff")]
    [InlineData("SqDiffNormed")]
    public async Task ExecuteAsync_WithSqDiffMethods_ShouldExposeCanonicalAndRawScores(string method)
    {
        var op = new Operator($"template_{method}", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", method, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.98, "double"));

        using var src = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        using var template = CreatePatternTemplate();
        CopyTemplate(src, template, 52, 48);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData.Should().ContainKeys("Score", "NormalizedScore", "RawResponse");

        var score = Convert.ToDouble(result.OutputData["Score"]);
        var normalizedScore = Convert.ToDouble(result.OutputData["NormalizedScore"]);
        var rawResponse = Convert.ToDouble(result.OutputData["RawResponse"]);

        score.Should().BeApproximately(normalizedScore, 1e-6);
        normalizedScore.Should().BeGreaterThan(0.98);
        rawResponse.Should().BeGreaterThanOrEqualTo(0.0);
        if (method == "SqDiffNormed")
        {
            rawResponse.Should().BeInRange(0.0, 0.02);
        }
        else
        {
            rawResponse.Should().BeLessThan(10.0);
        }

        var match = result.OutputData["Matches"]
            .Should().BeAssignableTo<IEnumerable<object>>()
            .Subject
            .Cast<Dictionary<string, object>>()
            .Single();

        Convert.ToDouble(match["NormalizedScore"]).Should().BeApproximately(normalizedScore, 1e-6);
        Convert.ToDouble(match["RawResponse"]).Should().BeApproximately(rawResponse, 1e-6);
    }

    [Theory]
    [InlineData("SqDiff")]
    [InlineData("SqDiffNormed")]
    public async Task ExecuteAsync_WithSqDiffMethodsAndMismatchedTemplate_ShouldRespectThreshold(string method)
    {
        var op = new Operator($"template_{method}_negative", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", method, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.95, "double"));

        using var src = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(src, new Point(60, 60), 18, Scalar.White, -1);

        using var template = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(template, new Rect(4, 4, 32, 32), Scalar.White, -1);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = src.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        Convert.ToDouble(result.OutputData["NormalizedScore"]).Should().Be(0.0);
        Convert.ToDouble(result.OutputData["RawResponse"]).Should().Be(0.0);
    }

    private static Mat CreatePatternTemplate()
    {
        var mat = new Mat(32, 32, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 4, 24, 24), Scalar.White, -1);
        Cv2.Line(mat, new Point(4, 16), new Point(28, 16), Scalar.Black, 2);
        Cv2.Circle(mat, new Point(16, 10), 4, Scalar.Black, -1);
        return mat;
    }

    private static Mat CreateLowContrastPadTemplate()
    {
        var mat = new Mat(36, 36, MatType.CV_8UC3, new Scalar(120, 120, 120));
        Cv2.Rectangle(mat, new Rect(4, 4, 28, 28), new Scalar(126, 126, 126), -1);
        return mat;
    }

    private static Mat CreateBroadPeakTemplate()
    {
        var mat = new Mat(72, 72, MatType.CV_8UC3, new Scalar(40, 40, 40));
        Cv2.Rectangle(mat, new Rect(6, 6, 60, 60), new Scalar(220, 220, 220), -1);
        Cv2.Rectangle(mat, new Rect(18, 18, 12, 12), new Scalar(70, 70, 70), -1);
        return mat;
    }

    private static void CopyTemplate(Mat scene, Mat template, int x, int y)
    {
        using var roi = new Mat(scene, new Rect(x, y, template.Width, template.Height));
        template.CopyTo(roi);
    }
}
