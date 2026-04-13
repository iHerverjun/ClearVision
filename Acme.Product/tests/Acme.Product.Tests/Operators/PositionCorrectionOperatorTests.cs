using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class PositionCorrectionOperatorTests
{
    private readonly PositionCorrectionOperator _operator = new(Substitute.For<ILogger<PositionCorrectionOperator>>());

    [Fact]
    public void OperatorType_ShouldBePositionCorrection()
    {
        Assert.Equal(OperatorType.PositionCorrection, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithTranslationMode_ShouldExposeCanonicalOutputs()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "CorrectionMode", "Translation" } });
        var inputs = new Dictionary<string, object>
        {
            { "ReferencePoint", new Position(20, 30) },
            { "BasePoint", new Position(10, 10) },
            { "RoiX", 15 },
            { "RoiY", 15 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, (int)result.OutputData!["CorrectedX"]);
        Assert.Equal(35, (int)result.OutputData["CorrectedY"]);
        Assert.Equal(10.0, Assert.IsType<double>(result.OutputData["AppliedOffsetX"]), 6);
        Assert.Equal(20.0, Assert.IsType<double>(result.OutputData["AppliedOffsetY"]), 6);
        Assert.Equal("Translation", Assert.IsType<string>(result.OutputData["CompensationMode"]));
        var matrix = Assert.IsType<double[][]>(result.OutputData["TransformMatrix"]);
        Assert.Equal(10.0, matrix[0][2], 6);
        Assert.Equal(20.0, matrix[1][2], 6);
    }

    [Fact]
    public async Task ExecuteAsync_WithTranslationRotationMode_ShouldKeepLegacyOffsetAndExposeAppliedOffset()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "TranslationRotation" },
            { "ReferenceAngle", 90.0 }
        });

        var inputs = new Dictionary<string, object>
        {
            { "ReferencePoint", new Position(10, 10) },
            { "BasePoint", new Position(5, 5) },
            { "RoiX", 8 },
            { "RoiY", 5 },
            { "CurrentAngle", 0.0 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, (int)result.OutputData!["CorrectedX"]);
        Assert.Equal(13, (int)result.OutputData["CorrectedY"]);
        Assert.Equal(5.0, Assert.IsType<double>(result.OutputData["OffsetX"]), 6);
        Assert.Equal(5.0, Assert.IsType<double>(result.OutputData["OffsetY"]), 6);
        Assert.Equal(2.0, Assert.IsType<double>(result.OutputData["AppliedOffsetX"]), 6);
        Assert.Equal(8.0, Assert.IsType<double>(result.OutputData["AppliedOffsetY"]), 6);

        var matrix = Assert.IsType<double[][]>(result.OutputData["TransformMatrix"]);
        var transformedX = (matrix[0][0] * 8) + (matrix[0][1] * 5) + matrix[0][2];
        var transformedY = (matrix[1][0] * 8) + (matrix[1][1] * 5) + matrix[1][2];
        Assert.Equal((double)(int)result.OutputData["CorrectedX"], transformedX, 6);
        Assert.Equal((double)(int)result.OutputData["CorrectedY"], transformedY, 6);

        var rotationCenter = Assert.IsType<Position>(result.OutputData["RotationCenter"]);
        Assert.Equal(5.0, rotationCenter.X, 6);
        Assert.Equal(5.0, rotationCenter.Y, 6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var validation = _operator.ValidateParameters(CreateOperator(new Dictionary<string, object> { { "CorrectionMode", "Invalid" } }));
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Metadata_ShouldDescribePixelSpaceUsage()
    {
        var meta = (OperatorMetaAttribute)Attribute.GetCustomAttribute(typeof(PositionCorrectionOperator), typeof(OperatorMetaAttribute))!;
        Assert.Contains("Pixel-space", meta.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("calibration", meta.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("PositionCorrection", OperatorType.PositionCorrection, 0, 0);
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
