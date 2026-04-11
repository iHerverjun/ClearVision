using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.ImageProcessing;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Industrial_Remediation")]
public class ColorDetectionOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithLabDeltaEMode_ShouldReturnNearZeroDeltaEForMatchingReference()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AnalysisMode", "LabDeltaE" },
            { "DeltaEMethod", "CIEDE2000" },
            { "RoiW", 40 },
            { "RoiH", 40 },
            { "WhiteBalanceTolerance", 5.0 }
        });

        using var image = CreateSolidColorImage(new Scalar(128, 128, 128));
        var lab = CieLabConverter.BgrToLab(128, 128, 128);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["ReferenceColor"] = new Dictionary<string, object>
        {
            { "L", lab.L },
            { "A", lab.A },
            { "B", lab.B }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["DeltaE"]) < 0.1);
        Assert.Equal("LabDeltaE", result.OutputData["AnalysisMode"]);
        Assert.Equal("Balanced", result.OutputData["WhiteBalanceStatus"]);
        Assert.True(result.OutputData.ContainsKey("MeanColor"));
    }

    [Fact]
    public async Task ExecuteAsync_WithHsvInspectionAndHueWrap_ShouldDetectRedCoverage()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AnalysisMode", "HsvInspection" },
            { "HueLow", 170 },
            { "HueHigh", 10 },
            { "SatLow", 150 },
            { "SatHigh", 255 },
            { "ValLow", 150 },
            { "ValHigh", 255 }
        });

        using var image = CreateSolidColorImage(new Scalar(0, 0, 255));
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["Coverage"]) > 0.95);
        Assert.Equal("HsvInspection", result.OutputData["AnalysisMode"]);
        Assert.Equal("HSV", result.OutputData["ColorSpace"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithLabDeltaEModeAndPartialReferenceInput_ShouldIgnoreIncompleteReference()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AnalysisMode", "LabDeltaE" },
            { "DeltaEMethod", "CIEDE2000" },
            { "RoiW", 40 },
            { "RoiH", 40 }
        });

        using var image = CreateSolidColorImage(new Scalar(255, 0, 0));
        var lab = CieLabConverter.BgrToLab(255, 0, 0);
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["ReferenceColor"] = new Dictionary<string, object>
        {
            { "L", lab.L },
            { "A", lab.A }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["DeltaE"]) < 0.1);

        var diagnostics = Assert.IsType<Dictionary<string, object>>(result.OutputData["Diagnostics"]);
        Assert.False(Convert.ToBoolean(diagnostics["ReferenceProvided"]));
    }

    [Fact]
    public async Task ExecuteAsync_WithLabDeltaEModeAndPartialReferenceParameters_ShouldIgnoreIncompleteReference()
    {
        var sut = CreateSut();
        using var image = CreateSolidColorImage(new Scalar(255, 0, 0));
        var lab = CieLabConverter.BgrToLab(255, 0, 0);
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AnalysisMode", "LabDeltaE" },
            { "DeltaEMethod", "CIEDE2000" },
            { "RefL", lab.L },
            { "RefA", lab.A },
            { "RoiW", 40 },
            { "RoiH", 40 }
        });

        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["DeltaE"]) < 0.1);

        var diagnostics = Assert.IsType<Dictionary<string, object>>(result.OutputData["Diagnostics"]);
        Assert.False(Convert.ToBoolean(diagnostics["ReferenceProvided"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidAnalysisMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AnalysisMode", "LabDistance" }
        });

        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ColorDetectionOperator CreateSut()
    {
        return new ColorDetectionOperator(Substitute.For<ILogger<ColorDetectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ColorDetection", OperatorType.ColorDetection, 0, 0);
        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(TestHelpers.CreateParameter(name, value, "string"));
            }
        }

        return op;
    }

    private static ImageWrapper CreateSolidColorImage(Scalar color)
    {
        var mat = new Mat(60, 60, MatType.CV_8UC3, color);
        return new ImageWrapper(mat);
    }
}
