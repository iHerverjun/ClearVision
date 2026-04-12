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
}
