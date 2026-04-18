using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class SurfaceDefectDetectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeSurfaceDefectDetection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.SurfaceDefectDetection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ReferenceDiffMode_ShouldDetectDefect()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ReferenceDiff" },
            { "Threshold", 10.0 },
            { "MinArea", 20 },
            { "MaxArea", 100000 },
            { "MorphCleanSize", 3 },
            { "ThresholdMode", "Auto" },
            { "AlignmentMode", "None" }
        });

        using var source = CreateSourceWithDefect();
        using var reference = CreateReferenceImage();
        var inputs = TestHelpers.CreateImageInputs(source);
        inputs["Reference"] = reference;

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["DefectCount"]) >= 1);
        Assert.True(result.OutputData.ContainsKey("DefectMask"));
        Assert.True(result.OutputData.ContainsKey("ResponseImage"));
        Assert.True(result.OutputData.ContainsKey("AlignmentScore"));
        Assert.True(result.OutputData.ContainsKey("Diagnostics"));
    }

    [Fact]
    public async Task ExecuteAsync_WithShiftedReference_ShouldExposeAcceptedTranslationDiagnostics()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ReferenceDiff" },
            { "Threshold", 20.0 },
            { "ThresholdMode", "ReferenceStats" },
            { "AlignmentMode", "PhaseCorrelation" },
            { "NormalizationMode", "LocalMean" },
            { "MinArea", 10 },
            { "MaxArea", 200000 }
        });

        using var reference = CreateStructuredReference();
        using var source = CreateShiftedSourceWithDefect();
        var inputs = TestHelpers.CreateImageInputs(source);
        inputs["Reference"] = reference;

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Diagnostics"));
        Assert.Equal(string.Empty, Convert.ToString(result.OutputData["RejectedReason"]));

        var diagnostics = Assert.IsType<Dictionary<string, object>>(result.OutputData["Diagnostics"]);
        Assert.True(Convert.ToDouble(result.OutputData["AlignmentScore"]) >= 0.0);
        Assert.True(Math.Abs(Convert.ToDouble(diagnostics["AlignmentShiftX"])) > 0.5);
        Assert.True(Math.Abs(Convert.ToDouble(diagnostics["AlignmentShiftY"])) > 0.5);
        Assert.Equal(string.Empty, Convert.ToString(diagnostics["RejectedReason"]));
    }

    [Fact]
    public async Task ExecuteAsync_WithLowResponseLargeShiftEstimate_ShouldRejectUntrustedTranslation()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ReferenceDiff" },
            { "Threshold", 20.0 },
            { "ThresholdMode", "ReferenceStats" },
            { "AlignmentMode", "PhaseCorrelation" },
            { "NormalizationMode", "LocalMean" },
            { "MinArea", 10 },
            { "MaxArea", 200000 }
        });

        using var reference = CreateSmoothReference();
        using var source = CreateSmoothSourceWithLocalizedDefect();
        var inputs = TestHelpers.CreateImageInputs(source);
        inputs["Reference"] = reference;

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
        Assert.Contains("response", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithRotatedReference_ShouldRejectTranslationOnlyAlignment()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "ReferenceDiff" },
            { "Threshold", 20.0 },
            { "ThresholdMode", "ReferenceStats" },
            { "AlignmentMode", "PhaseCorrelation" },
            { "NormalizationMode", "LocalMean" },
            { "MinArea", 10 },
            { "MaxArea", 200000 }
        });

        using var reference = CreateStructuredReference();
        using var source = CreateRotatedSourceWithDefect();
        var inputs = TestHelpers.CreateImageInputs(source);
        inputs["Reference"] = reference;

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
        Assert.Contains("translation", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Method", "PhaseOnly" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static SurfaceDefectDetectionOperator CreateSut()
    {
        return new SurfaceDefectDetectionOperator(Substitute.For<ILogger<SurfaceDefectDetectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("SurfaceDefectDetection", OperatorType.SurfaceDefectDetection, 0, 0);

        if (parameters == null)
        {
            return op;
        }

        foreach (var (name, value) in parameters)
        {
            op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
        }

        return op;
    }

    private static ImageWrapper CreateSourceWithDefect()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(40, 40, 30, 30), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateReferenceImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateStructuredReference()
    {
        var mat = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(40, 40, 50, 30), Scalar.White, -1);
        Cv2.Circle(mat, new Point(110, 100), 14, new Scalar(180, 180, 180), -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSmoothReference()
    {
        var mat = new Mat(160, 160, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(80, 80), 18, new Scalar(80, 80, 80), -1);
        Cv2.GaussianBlur(mat, mat, new Size(0, 0), 60, 60);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateShiftedSourceWithDefect()
    {
        using var reference = CreateStructuredReference();
        var source = new Mat();
        using var transform = new Mat(2, 3, MatType.CV_64FC1, Scalar.All(0));
        transform.Set(0, 0, 1.0);
        transform.Set(1, 1, 1.0);
        transform.Set(0, 2, 4.0);
        transform.Set(1, 2, -3.0);
        Cv2.WarpAffine(reference.MatReadOnly, source, transform, reference.MatReadOnly.Size(), InterpolationFlags.Linear, BorderTypes.Constant);
        Cv2.Rectangle(source, new Rect(120, 30, 12, 12), Scalar.White, -1);
        return new ImageWrapper(source);
    }

    private static ImageWrapper CreateSmoothSourceWithLocalizedDefect()
    {
        using var reference = CreateSmoothReference();
        var source = reference.MatReadOnly.Clone();
        Cv2.Rectangle(source, new Rect(120, 30, 12, 12), Scalar.White, -1);
        return new ImageWrapper(source);
    }

    private static ImageWrapper CreateRotatedSourceWithDefect()
    {
        using var reference = CreateStructuredReference();
        var source = new Mat();
        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(80, 80), 11.0, 1.0);
        Cv2.WarpAffine(reference.MatReadOnly, source, rotation, reference.MatReadOnly.Size(), InterpolationFlags.Linear, BorderTypes.Constant);
        Cv2.Rectangle(source, new Rect(120, 30, 12, 12), Scalar.White, -1);
        return new ImageWrapper(source);
    }
}
