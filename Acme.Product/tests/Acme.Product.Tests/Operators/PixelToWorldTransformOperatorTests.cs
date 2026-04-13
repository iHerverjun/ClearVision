using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.TestData;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class PixelToWorldTransformOperatorTests
{
    private readonly PixelToWorldTransformOperator _operator;

    public PixelToWorldTransformOperatorTests()
    {
        _operator = new PixelToWorldTransformOperator(Substitute.For<ILogger<PixelToWorldTransformOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePixelToWorldTransform()
    {
        _operator.OperatorType.Should().Be(OperatorType.PixelToWorldTransform);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCalibrationData_ShouldReturnFailure()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCalibration_ShouldReturnSuccess()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(100, 100) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithPixelToWorldMode_ShouldTransformPoints()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("TransformMode", "PixelToWorld"));
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(160, 120) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("TransformedPoints");
    }

    [Fact]
    public async Task ExecuteAsync_WithWorldToPixelMode_ShouldTransformPoints()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("TransformMode", "WorldToPixel"));
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(0, 0) };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithValidUnitScale_ShouldBeValid()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("UnitScale", 0.001));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidUnitScale_ShouldBeInvalid()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("UnitScale", 0.0));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPointsJson_ShouldFailClosed()
    {
        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();
        inputs["Points"] = "[{\"X\":100}]";

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Points[0]");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedDistortionModel_ShouldFail()
    {
        const string rayPlaneBundleWithKannala = """
                                                 {
                                                   "schemaVersion": 2,
                                                   "calibrationKind": "cameraIntrinsics",
                                                   "transformModel": "none",
                                                   "sourceFrame": "image",
                                                   "targetFrame": "world",
                                                   "unit": "mm",
                                                   "intrinsics": {
                                                     "cameraMatrix": [
                                                       [500.0, 0.0, 160.0],
                                                       [0.0, 500.0, 120.0],
                                                       [0.0, 0.0, 1.0]
                                                     ]
                                                   },
                                                   "distortion": {
                                                     "model": "kannalaBrandt",
                                                     "coefficients": [0.1, 0.01, 0.0, 0.0]
                                                   },
                                                   "transform3D": {
                                                     "model": "rigid3D",
                                                     "matrix": [
                                                       [1.0, 0.0, 0.0, 0.0],
                                                       [0.0, 1.0, 0.0, 0.0],
                                                       [0.0, 0.0, 1.0, 100.0],
                                                       [0.0, 0.0, 0.0, 1.0]
                                                     ]
                                                   },
                                                   "quality": {
                                                     "accepted": true,
                                                     "meanError": 0.10,
                                                     "maxError": 0.20,
                                                     "inlierCount": 12,
                                                     "totalSampleCount": 12,
                                                     "diagnostics": []
                                                   },
                                                   "producerOperator": "test"
                                                 }
                                                 """;

        var op = new Operator("PixelToWorldTransform", OperatorType.PixelToWorldTransform, 0, 0);
        using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["CalibrationData"] = rayPlaneBundleWithKannala;
        inputs["Points"] = new List<Acme.Product.Core.ValueObjects.Position> { new(100, 100) };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("KannalaBrandt");
    }
}
