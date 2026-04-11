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
public class EdgePairDefectOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeEdgePairDefect()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.EdgePairDefect, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedParallelLines_ShouldHaveZeroDefects()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ExpectedWidth", 20.0 },
            { "Tolerance", 1.0 },
            { "NumSamples", 25 },
            { "EdgeMethod", "Canny" }
        });

        using var image = CreateBlankImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(10, 20, 110, 20);
        inputs["Line2"] = new LineData(10, 40, 110, 40);

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(0, Convert.ToInt32(result.OutputData!["DefectCount"]));
        Assert.Equal(0.0, Convert.ToDouble(result.OutputData["MaxDeviation"]), 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutInputLines_ShouldPreferPairNearExpectedWidth()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ExpectedWidth", 24.0 },
            { "Tolerance", 4.0 },
            { "NumSamples", 40 },
            { "EdgeMethod", "Canny" }
        });

        using var image = CreateAutoDetectPreferenceImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.InRange(Convert.ToDouble(result.OutputData!["MaxDeviation"]), 0.0, 15.0);

        var deviations = Assert.IsAssignableFrom<IReadOnlyList<double>>(result.OutputData["Deviations"]);
        Assert.Equal(40, deviations.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentSampleCounts_ShouldKeepDefectSegmentCountStable()
    {
        var sut = CreateSut();

        async Task<(int DefectCount, int DeviationCount)> RunOnceAsync(int sampleCount)
        {
            var op = CreateOperator(new Dictionary<string, object>
            {
                { "ExpectedWidth", 22.0 },
                { "Tolerance", 2.0 },
                { "NumSamples", sampleCount },
                { "EdgeMethod", "Canny" }
            });

            using var image = CreateBlankImage();
            var inputs = TestHelpers.CreateImageInputs(image);
            inputs["Line1"] = new LineData(10, 20, 110, 20);
            inputs["Line2"] = new LineData(10, 46, 110, 46);

            var result = await sut.ExecuteAsync(op, inputs);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputData);
            var deviations = Assert.IsAssignableFrom<IReadOnlyList<double>>(result.OutputData!["Deviations"]);

            return (Convert.ToInt32(result.OutputData["DefectCount"]), deviations.Count);
        }

        var lowSample = await RunOnceAsync(25);
        var highSample = await RunOnceAsync(250);

        Assert.True(lowSample.DefectCount >= 1);
        Assert.True(highSample.DefectCount >= 1);
        Assert.InRange(Math.Abs(highSample.DefectCount - lowSample.DefectCount), 0, 1);
        Assert.Equal(25, lowSample.DeviationCount);
        Assert.Equal(250, highSample.DeviationCount);
    }

    [Fact]
    public void ValidateParameters_WithInvalidEdgeMethod_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "EdgeMethod", "Laplacian" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static EdgePairDefectOperator CreateSut()
    {
        return new EdgePairDefectOperator(Substitute.For<ILogger<EdgePairDefectOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("EdgePairDefect", OperatorType.EdgePairDefect, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateBlankImage()
    {
        var mat = new Mat(120, 140, MatType.CV_8UC3, Scalar.Black);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateAutoDetectPreferenceImage()
    {
        var mat = new Mat(160, 240, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 30), new Point(220, 30), Scalar.White, 2);
        Cv2.Line(mat, new Point(20, 54), new Point(220, 54), Scalar.White, 2);
        Cv2.Line(mat, new Point(20, 118), new Point(220, 118), Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}
