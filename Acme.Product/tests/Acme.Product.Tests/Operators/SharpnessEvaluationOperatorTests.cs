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

    private static ImageWrapper CreateBlurredPatternImage()
    {
        using var sharp = CreatePatternImage();
        var src = sharp.GetMat();
        var blurred = new Mat();
        Cv2.GaussianBlur(src, blurred, new Size(21, 21), 8);
        return new ImageWrapper(blurred);
    }
}
