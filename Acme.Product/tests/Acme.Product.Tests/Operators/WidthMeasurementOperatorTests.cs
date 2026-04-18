using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class WidthMeasurementOperatorTests
{
    private readonly WidthMeasurementOperator _operator;

    public WidthMeasurementOperatorTests()
    {
        _operator = new WidthMeasurementOperator(Substitute.For<ILogger<WidthMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeWidthMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.WidthMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithManualLinesAndRealEdges_ShouldReturnExpectedWidth()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["MeasureMode"] = "ManualLines",
            ["SampleCount"] = 20,
            ["MultiScanCount"] = 24,
            ["RobustMode"] = true,
            ["OutlierSigmaK"] = 3.0,
            ["MinValidSamples"] = 8
        });

        using var image = CreateEdgeWidthImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(48, 25, 48, 135);
        inputs["Line2"] = new LineData(102, 25, 102, 135);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Width"]).Should().BeApproximately(50.0, 4.0);
        result.OutputData["SampleCount"].Should().Be(20);
        result.OutputData["MultiScanCount"].Should().Be(24);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutEdgeEvidence_ShouldReturnFailure()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["MeasureMode"] = "ManualLines",
            ["SampleCount"] = 20,
            ["MultiScanCount"] = 20
        });

        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(20, 20, 20, 120);
        inputs["Line2"] = new LineData(40, 20, 40, 120);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NoFeature");
    }

    [Fact]
    public void ValidateParameters_WithMultiScanLessThanSampleCount_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["SampleCount"] = 20,
            ["MultiScanCount"] = 12
        });

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithIndustrialManualLines_ShouldMeetIndustrialTolerance()
    {
        const double expectedWidth = 50.0;
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["MeasureMode"] = "ManualLines",
            ["SampleCount"] = 32,
            ["MultiScanCount"] = 48,
            ["RobustMode"] = true,
            ["OutlierSigmaK"] = 3.0,
            ["MinValidSamples"] = 24
        });

        using var image = IndustrialMeasurementSceneFactory.CreateFilledVerticalStripeImage(
            width: 220,
            height: 180,
            leftX: 70.0,
            rightX: 120.0,
            topY: 18,
            bottomY: 162);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(68f, 20f, 68f, 160f);
        inputs["Line2"] = new LineData(122f, 20f, 122f, 160f);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Width"]).Should().BeApproximately(expectedWidth, 0.15);
        Convert.ToDouble(result.OutputData["MeanWidth"]).Should().BeApproximately(expectedWidth, 0.15);
        Convert.ToDouble(result.OutputData["P95Width"]).Should().BeLessThan(expectedWidth + 0.30);
        Convert.ToDouble(result.OutputData["StdDev"]).Should().BeLessThan(0.15);
        Convert.ToDouble(result.OutputData["ValidSampleRate"]).Should().BeGreaterThan(0.95);
        Convert.ToInt32(result.OutputData["ValidSampleCount"]).Should().BeGreaterOrEqualTo(24);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Width", OperatorType.WidthMeasurement, 0, 0);
        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateEdgeWidthImage()
    {
        var mat = new Mat(180, 180, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 20, 50, 130), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
