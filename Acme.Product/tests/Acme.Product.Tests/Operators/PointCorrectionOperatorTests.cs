using Acme.Product.Core.Entities;
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

        var matrix = Assert.IsType<double[][]>(result.OutputData["TransformMatrix"]);
        Assert.Equal(1.0, matrix[0][2], 6);
        Assert.Equal(2.0, matrix[1][2], 6);
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
