using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class SharpnessEvaluationOperatorTests
{
    private readonly SharpnessEvaluationOperator _operator;

    public SharpnessEvaluationOperatorTests()
    {
        _operator = new SharpnessEvaluationOperator(Substitute.For<ILogger<SharpnessEvaluationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeSharpnessEvaluation()
    {
        _operator.OperatorType.Should().Be(OperatorType.SharpnessEvaluation);
    }

    [Fact]
    public async Task ExecuteAsync_PerMethodThresholds_ShouldStillRankSharpImageHigher()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["Method"] = "Laplacian",
            ["ThresholdMode"] = "PerMethodDefault"
        });

        using var sharp = CreatePatternImage();
        using var blur = CreateBlurredPatternImage();

        var sharpResult = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(sharp));
        var blurResult = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(blur));

        sharpResult.IsSuccess.Should().BeTrue(sharpResult.ErrorMessage);
        blurResult.IsSuccess.Should().BeTrue(blurResult.ErrorMessage);
        Convert.ToDouble(sharpResult.OutputData!["Score"]).Should().BeGreaterThan(Convert.ToDouble(blurResult.OutputData!["Score"]));
        sharpResult.OutputData.Should().ContainKeys("ThresholdUsed", "DecisionReady");
    }

    [Fact]
    public async Task ExecuteAsync_ManualThreshold_ShouldExposeStableMarginAndUncertainty()
    {
        using var sharp = CreatePatternImage();
        using var blur = CreateBlurredPatternImage(8.0);

        var probeOp = CreateOperator(new Dictionary<string, object>
        {
            ["Method"] = "Laplacian",
            ["ThresholdMode"] = "Manual",
            ["Threshold"] = 0.0
        });

        var sharpProbe = await _operator.ExecuteAsync(probeOp, TestHelpers.CreateImageInputs(sharp.AddRef()));
        var blurProbe = await _operator.ExecuteAsync(probeOp, TestHelpers.CreateImageInputs(blur.AddRef()));

        var sharpScore = Convert.ToDouble(sharpProbe.OutputData!["Score"]);
        var blurScore = Convert.ToDouble(blurProbe.OutputData!["Score"]);
        var threshold = (sharpScore + blurScore) / 2.0;

        var evalOp = CreateOperator(new Dictionary<string, object>
        {
            ["Method"] = "Laplacian",
            ["ThresholdMode"] = "Manual",
            ["Threshold"] = threshold
        });

        var sharpResult = await _operator.ExecuteAsync(evalOp, TestHelpers.CreateImageInputs(sharp.AddRef()));
        var blurResult = await _operator.ExecuteAsync(evalOp, TestHelpers.CreateImageInputs(blur.AddRef()));

        sharpResult.IsSuccess.Should().BeTrue(sharpResult.ErrorMessage);
        blurResult.IsSuccess.Should().BeTrue(blurResult.ErrorMessage);
        Convert.ToBoolean(sharpResult.OutputData!["IsSharp"]).Should().BeTrue();
        Convert.ToBoolean(blurResult.OutputData!["IsSharp"]).Should().BeFalse();
        Convert.ToDouble(sharpResult.OutputData["MarginToThreshold"]).Should().BeGreaterThan(0.0);
        Convert.ToDouble(blurResult.OutputData["MarginToThreshold"]).Should().BeLessThan(0.0);
        Convert.ToDouble(sharpResult.OutputData["NormalizedScore"]).Should().BeApproximately(
            Convert.ToDouble(sharpResult.OutputData["Score"]) / threshold,
            1e-6);
        Convert.ToInt32(sharpResult.OutputData["TileCount"]).Should().BeGreaterThan(0);
        Convert.ToDouble(sharpResult.OutputData["UncertaintyPx"]).Should().BeGreaterThanOrEqualTo(0.0);
        sharpScore.Should().BeGreaterThan(blurScore * 3.0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidThresholdMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { ["ThresholdMode"] = "Auto" });
        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Sharpness", OperatorType.SharpnessEvaluation, 0, 0);
        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreatePatternImage()
    {
        var mat = new Mat(200, 200, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(10, 10), new Point(190, 10), Scalar.White, 2);
        Cv2.Line(mat, new Point(10, 30), new Point(190, 180), Scalar.White, 2);
        Cv2.Rectangle(mat, new Rect(60, 60, 80, 80), Scalar.White, 2);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateBlurredPatternImage(double sigma = 8.0)
    {
        using var sharp = CreatePatternImage();
        var src = sharp.GetMat();
        var blurred = new Mat();
        Cv2.GaussianBlur(src, blurred, new Size(21, 21), sigma);
        return new ImageWrapper(blurred);
    }
}
