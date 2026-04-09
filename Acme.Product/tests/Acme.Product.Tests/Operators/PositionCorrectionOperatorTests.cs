using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Core.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class PositionCorrectionOperatorTests
{
    private readonly PositionCorrectionOperator _operator;

    public PositionCorrectionOperatorTests()
    {
        _operator = new PositionCorrectionOperator(Substitute.For<ILogger<PositionCorrectionOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBePositionCorrection()
    {
        Assert.Equal(OperatorType.PositionCorrection, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithTranslationMode_ShouldCorrectRoi()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "Translation" }
        });

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
    }

    [Fact]
    public async Task ExecuteAsync_WithTranslationRotationMode_ShouldRotate()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CorrectionMode", "TranslationRotation" },
            { "ReferenceAngle", 90.0 }
        });

        var inputs = new Dictionary<string, object>
        {
            { "ReferencePoint", new Position(0, 0) },
            { "BasePoint", new Position(0, 0) },
            { "RoiX", 10 },
            { "RoiY", 0 },
            { "CurrentAngle", 0.0 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.InRange((int)result.OutputData!["CorrectedX"], -1, 1);
        Assert.InRange((int)result.OutputData["CorrectedY"], 9, 11);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "CorrectionMode", "Invalid" } });

        var validation = _operator.ValidateParameters(op);

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
