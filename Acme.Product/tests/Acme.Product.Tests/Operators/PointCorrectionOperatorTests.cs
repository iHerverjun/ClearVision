using Acme.Product.Core.Entities;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class PointCorrectionOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBePointCorrection()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.PointCorrection, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_TranslationOnlyWithMillimeterOutput_ShouldReturnExpectedCorrection()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "TranslationOnly" },
            { "OutputUnit", "mm" },
            { "PixelSize", 0.1 }
        });

        var inputs = new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(100, 50) },
            { "ReferencePoint", new Position(110, 70) }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(1.0, Convert.ToDouble(result.OutputData!["CorrectionX"]), 6);
        Assert.Equal(2.0, Convert.ToDouble(result.OutputData["CorrectionY"]), 6);
        Assert.Equal(0.0, Convert.ToDouble(result.OutputData["CorrectionAngle"]), 6);
        Assert.Equal("Pixel", Assert.IsType<string>(result.OutputData["TransformUnit"]));

        var matrix = Assert.IsType<double[][]>(result.OutputData["TransformMatrix"]);
        Assert.Equal(10.0, matrix[0][2], 6);
        Assert.Equal(20.0, matrix[1][2], 6);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRequiredPoints_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>());

        Assert.False(result.IsSuccess);
        Assert.Contains("DetectedPoint", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "CorrectionMode", "Rigid3D" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCorrectionMode_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "CorrectionMode", "Rigid3D" } });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(1, 1) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("CorrectionMode", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidOutputUnit_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "OutputUnit", "cm" } });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(1, 1) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("OutputUnit", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_TranslationRotationWithMillimeterOutput_ShouldScaleTranslationOnly()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "TranslationRotation" },
            { "OutputUnit", "mm" },
            { "PixelSize", 0.2 },
            { "DetectedAngle", 10.0 },
            { "ReferenceAngle", 20.0 }
        });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(10, 0) },
            { "ReferencePoint", new Position(20, 10) }
        });

        Assert.True(result.IsSuccess);
        var matrix = Assert.IsType<double[][]>(result.OutputData!["TransformMatrix"]);
        Assert.Equal("Pixel", Assert.IsType<string>(result.OutputData["TransformUnit"]));
        Assert.Equal(10.0, Convert.ToDouble(result.OutputData["CorrectionAngle"]), 6);
        Assert.Equal(20 - ((Math.Cos(Math.PI / 18) * 10) - (Math.Sin(Math.PI / 18) * 0)), matrix[0][2], 6);
        Assert.Equal(10 - ((Math.Sin(Math.PI / 18) * 10) + (Math.Cos(Math.PI / 18) * 0)), matrix[1][2], 6);
        Assert.Equal(Math.Cos(Math.PI / 18), matrix[0][0], 6);
        Assert.Equal(Math.Cos(Math.PI / 18), matrix[1][1], 6);
    }

    [Fact]
    public async Task ExecuteAsync_TranslationRotation_ShouldNormalizeAngleToSignedRange()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "TranslationRotation" },
            { "DetectedAngle", -179.0 },
            { "ReferenceAngle", 179.0 }
        });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(0, 0) }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(-2.0, Convert.ToDouble(result.OutputData!["CorrectionAngle"]), 6);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonFiniteAngle_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "DetectedAngle", "NaN" }
        });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(1, 1) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("DetectedAngle", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonFiniteDetectedPoint_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(double.PositiveInfinity, 0) },
            { "ReferencePoint", new Position(1, 1) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("DetectedPoint", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxAllowedDistanceExceeded_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MaxAllowedDistance", 5.0 }
        });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(10, 0) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("MaxAllowedDistance", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowercaseDictionaryKeys_ShouldSucceed()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Dictionary<string, object> { ["x"] = 2.0, ["y"] = 3.0 } },
            { "ReferencePoint", new Dictionary<string, object> { ["x"] = 7.0, ["y"] = 11.0 } }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(5.0, Convert.ToDouble(result.OutputData!["CorrectionX"]), 6);
        Assert.Equal(8.0, Convert.ToDouble(result.OutputData["CorrectionY"]), 6);
    }

    [Fact]
    public async Task ExecuteAsync_WithMillimeterOutput_ShouldInterpretMaxAllowedDistanceInMillimeter()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "OutputUnit", "mm" },
            { "PixelSize", 0.5 },
            { "MaxAllowedDistance", 4.0 }
        });

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "DetectedPoint", new Position(0, 0) },
            { "ReferencePoint", new Position(10, 0) }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("Distance=5.000000", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMaxAllowedDistance_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "MaxAllowedDistance", "NaN" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("MaxAllowedDistance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Metadata_ShouldDescribeCalibrationRequirement()
    {
        var meta = (OperatorMetaAttribute)Attribute.GetCustomAttribute(typeof(PointCorrectionOperator), typeof(OperatorMetaAttribute))!;
        Assert.Contains("Pixel-space", meta.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("calibration", meta.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static PointCorrectionOperator CreateSut()
    {
        return new PointCorrectionOperator(Substitute.For<ILogger<PointCorrectionOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PointCorrection", OperatorType.PointCorrection, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
