using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

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
        Assert.Equal(OperatorType.SharpnessEvaluation, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_SharpImage_ShouldScoreHigherThanBlurredImage()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "Laplacian" },
            { "Threshold", 0.0 }
        });

        using var sharp = CreatePatternImage();
        using var blur = CreateBlurredPatternImage();

        var sharpResult = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(sharp));
        var blurResult = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(blur));

        Assert.True(sharpResult.IsSuccess);
        Assert.True(blurResult.IsSuccess);

        var sharpScore = Convert.ToDouble(sharpResult.OutputData!["Score"]);
        var blurScore = Convert.ToDouble(blurResult.OutputData!["Score"]);

        Assert.True(sharpScore > blurScore);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "Unknown" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
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
