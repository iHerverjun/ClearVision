using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.ImageProcessing;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class ColorMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeColorMeasurement()
    {
        CreateSut().OperatorType.Should().Be(OperatorType.ColorMeasurement);
    }

    [Fact]
    public async Task ExecuteAsync_LabDeltaEMode_ShouldReturnDeltaEAndLabMean()
    {
        var sut = CreateSut();
        var referenceLab = CieLabConverter.BgrToLab(20, 40, 180);
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["MeasurementMode"] = "LabDeltaE",
            ["DeltaEMethod"] = "CIEDE2000",
            ["RoiW"] = 40,
            ["RoiH"] = 40
        });

        using var image = CreateSolidColorImage(new Scalar(30, 30, 200));
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["ReferenceColor"] = new Dictionary<string, object>
        {
            ["L"] = referenceLab.L,
            ["A"] = referenceLab.A,
            ["B"] = referenceLab.B
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("LabMean");
        var expectedLab = CieLabConverter.BgrToLab(30, 30, 200);
        var expectedDeltaE = ColorDifference.DeltaE00(expectedLab, referenceLab);
        var labMean = result.OutputData!["LabMean"].Should().BeOfType<Dictionary<string, object>>().Subject;
        Convert.ToDouble(labMean["L"]).Should().BeApproximately(expectedLab.L, 1e-6);
        Convert.ToDouble(labMean["A"]).Should().BeApproximately(expectedLab.A, 1e-6);
        Convert.ToDouble(labMean["B"]).Should().BeApproximately(expectedLab.B, 1e-6);
        Convert.ToDouble(result.OutputData["DeltaE"]).Should().BeApproximately(expectedDeltaE, 1e-6);
        Convert.ToDouble(result.OutputData["DeltaEStdDev"]).Should().BeApproximately(0.0, 1e-9);
        Convert.ToDouble(result.OutputData["DeltaEStdError"]).Should().BeApproximately(0.0, 1e-9);
        result.OutputData["MeasurementMode"].Should().Be("LabDeltaE");
    }

    [Fact]
    public async Task ExecuteAsync_HsvStatsMode_ShouldUseCircularHueMean()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            ["MeasurementMode"] = "HsvStats"
        });

        using var image = CreateHueWrapImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["MeasurementMode"].Should().Be("HsvStats");
        result.OutputData["HueValid"].Should().Be(true);
        var hueMean = Convert.ToDouble(result.OutputData["HueMean"]);
        (hueMean < 20.0 || hueMean > 340.0).Should().BeTrue();
        Convert.ToDouble(result.OutputData["HueCircularStdDeg"]).Should().BeLessThan(5.0);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(1.0);
        double.IsNaN(Convert.ToDouble(result.OutputData["DeltaE"])).Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMeasurementMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { ["MeasurementMode"] = "RGB" });
        sut.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ColorMeasurementOperator CreateSut()
    {
        return new ColorMeasurementOperator(Substitute.For<ILogger<ColorMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ColorMeasurement", OperatorType.ColorMeasurement, 0, 0);
        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateSolidColorImage(Scalar bgr)
    {
        return new ImageWrapper(new Mat(60, 60, MatType.CV_8UC3, bgr));
    }

    private static ImageWrapper CreateHueWrapImage()
    {
        using var hsv = new Mat(60, 60, MatType.CV_8UC3);
        for (var y = 0; y < hsv.Rows; y++)
        {
            for (var x = 0; x < hsv.Cols; x++)
            {
                hsv.Set(y, x, x < 30 ? new Vec3b(1, 255, 255) : new Vec3b(179, 255, 255));
            }
        }

        var mat = new Mat();
        Cv2.CvtColor(hsv, mat, ColorConversionCodes.HSV2BGR);
        return new ImageWrapper(mat);
    }
}
