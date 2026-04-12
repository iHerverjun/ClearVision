using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
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
            ["L"] = 0.0,
            ["A"] = 0.0,
            ["B"] = 0.0
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("LabMean");
        Convert.ToDouble(result.OutputData!["DeltaE"]).Should().BeGreaterThan(0.0);
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
        var mat = new Mat(60, 60, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                mat.Set(y, x, x < 30 ? new Vec3b(0, 0, 255) : new Vec3b(10, 10, 255));
            }
        }

        return new ImageWrapper(mat);
    }
}
