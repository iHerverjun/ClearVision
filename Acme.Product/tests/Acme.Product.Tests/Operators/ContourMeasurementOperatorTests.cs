using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ContourMeasurementOperatorTests
{
    private readonly ContourMeasurementOperator _operator;

    public ContourMeasurementOperatorTests()
    {
        _operator = new ContourMeasurementOperator(Substitute.For<ILogger<ContourMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeContourMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.ContourMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        using var image = TestHelpers.CreateShapeTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithGrayImage_ShouldReturnSuccess()
    {
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        using var image = TestHelpers.CreateGrayShapeTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("ContourCount");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithIndustrialRotatedRectangle_ShouldMeetIndustrialTolerance()
    {
        const double expectedArea = 88.0 * 46.0;
        var op = new Operator("contour", OperatorType.ContourMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 96.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 2000, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledRotatedRectangleImage(
            width: 320,
            height: 240,
            center: new Point2d(160.0, 120.0),
            size: new Size2d(88.0, 46.0),
            angleDeg: 27.0,
            supersample: 16);

        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var measuredArea = Convert.ToDouble(result.OutputData!["Area"]);
        var areaRelativeError = Math.Abs(measuredArea - expectedArea) / expectedArea;
        areaRelativeError.Should().BeLessThan(0.015);
        Convert.ToDouble(result.OutputData["CenterX"]).Should().BeApproximately(160.0, 0.60);
        Convert.ToDouble(result.OutputData["CenterY"]).Should().BeApproximately(120.0, 0.60);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.20);
        Convert.ToInt32(result.OutputData["ContourPointCount"]).Should().BeGreaterThan(200);
    }
}
