using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class CircleMeasurementOperatorTests
{
    private readonly CircleMeasurementOperator _operator;

    public CircleMeasurementOperatorTests()
    {
        _operator = new CircleMeasurementOperator(Substitute.For<ILogger<CircleMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeCircleMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.CircleMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        using var image = TestHelpers.CreateShapeTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
        result.OutputData.Should().ContainKey("StatusCode");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithGrayShapeImage_ShouldReturnSuccess()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        using var image = TestHelpers.CreateGrayShapeTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithFitEllipseAndNoFeature_ShouldReturnFailure()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "FitEllipse", "enum"));

        using var image = TestHelpers.CreateGrayTestImage(200, 200, 0);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("[NoFeature]");
    }

    [Fact]
    public async Task ExecuteAsync_WithIndustrialCircleScene_ShouldMeetIndustrialTolerance()
    {
        var op = new Operator("circle", OperatorType.CircleMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "FitEllipse", "enum"));
        op.AddParameter(TestHelpers.CreateParameter("MinRadius", 60, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MaxRadius", 84, "int"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledCircleImage(
            width: 420,
            height: 320,
            center: new Point2d(210.0, 154.0),
            radius: 72.0,
            supersample: 16);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var center = result.OutputData!["Center"].Should().BeOfType<Position>().Subject;
        var radius = Convert.ToDouble(result.OutputData["Radius"]);
        var expectedArea = Math.PI * 72.0 * 72.0;
        var measuredArea = Math.PI * radius * radius;
        var areaRelativeError = Math.Abs(measuredArea - expectedArea) / expectedArea;
        areaRelativeError.Should().BeLessThan(0.015);
        center.X.Should().BeApproximately(210.0, 0.50);
        center.Y.Should().BeApproximately(154.0, 0.50);
        Convert.ToDouble(result.OutputData["Circularity"]).Should().BeGreaterThan(0.89);
    }
}
