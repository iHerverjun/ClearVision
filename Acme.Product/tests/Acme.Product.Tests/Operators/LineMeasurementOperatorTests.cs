using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class LineMeasurementOperatorTests
{
    private readonly LineMeasurementOperator _operator;

    public LineMeasurementOperatorTests()
    {
        _operator = new LineMeasurementOperator(Substitute.For<ILogger<LineMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeLineMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.LineMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithHoughLines_ShouldReportLineDirectionInsteadOfNormalAngle()
    {
        var op = new Operator("line", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "HoughLines", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 80, "int"));

        using var image = CreateHorizontalLineImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var angle = Convert.ToDouble(result.OutputData!["Angle"]);
        angle.Should().BeApproximately(0.0, 5.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithFitLine_ShouldEmitResidualDiagnostics()
    {
        var op = new Operator("line", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "FitLine", "string"));
        op.AddParameter(TestHelpers.CreateParameter("MinLength", 50.0, "double"));

        using var image = CreateDiagonalLineImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("ResidualMean");
        result.OutputData.Should().ContainKey("ResidualMax");
        result.OutputData!["Line"].Should().BeOfType<LineData>();
        Convert.ToDouble(result.OutputData["ResidualMax"]).Should().BeLessThan(5.0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldBeInvalid()
    {
        var op = new Operator("line", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "Ransac", "enum"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateHorizontalLineImage()
    {
        var mat = new Mat(200, 240, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 100), new Point(220, 100), Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateDiagonalLineImage()
    {
        var mat = new Mat(220, 240, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 180), new Point(220, 40), Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    [Fact]
    public async Task ExecuteAsync_WithIndustrialLineScene_ShouldMeetIndustrialTolerance()
    {
        var op = new Operator("line", OperatorType.LineMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "ProbabilisticHough", "string"));
        op.AddParameter(TestHelpers.CreateParameter("MinLength", 160.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 70, "int"));

        var expectedStart = new Point2d(20.0, 100.0);
        var expectedEnd = new Point2d(220.0, 100.0);
        using var image = IndustrialMeasurementSceneFactory.CreateLineImage(
            width: 240,
            height: 220,
            start: expectedStart,
            end: expectedEnd,
            thicknessPx: 2.0);

        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var line = result.OutputData!["Line"].Should().BeOfType<LineData>().Subject;
        var expectedAngle = Math.Atan2(expectedEnd.Y - expectedStart.Y, expectedEnd.X - expectedStart.X) * 180.0 / Math.PI;
        Convert.ToDouble(result.OutputData["Angle"]).Should().BeApproximately(expectedAngle, 0.05);
        Convert.ToDouble(result.OutputData["ResidualMean"]).Should().BeLessThan(0.20);
        Convert.ToDouble(result.OutputData["ResidualMax"]).Should().BeLessThan(0.20);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.20);
        line.Length.Should().BeGreaterOrEqualTo(200.0f);
    }
}
