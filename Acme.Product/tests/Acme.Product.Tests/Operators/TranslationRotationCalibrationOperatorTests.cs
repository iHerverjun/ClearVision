using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class TranslationRotationCalibrationOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeTranslationRotationCalibration()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.TranslationRotationCalibration, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithAffineLikePoints_ShouldProduceLowErrorTransform()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Method", "LeastSquares" },
            { "CalibrationPoints", BuildCalibrationPointsJson() }
        });

        var result = await sut.ExecuteAsync(op, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);

        var matrix = Assert.IsType<double[][]>(result.OutputData!["TransformMatrix"]);
        Assert.Equal(1.0, matrix[0][0], 3);
        Assert.Equal(0.0, matrix[0][1], 3);
        Assert.Equal(10.0, matrix[0][2], 3);
        Assert.Equal(0.0, matrix[1][0], 3);
        Assert.Equal(1.0, matrix[1][1], 3);
        Assert.Equal(20.0, matrix[1][2], 3);
        Assert.True(Convert.ToDouble(result.OutputData["CalibrationError"]) < 1e-6);
    }

    [Fact]
    public void ValidateParameters_WithTooFewPoints_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CalibrationPoints", "[{\"imageX\":0,\"imageY\":0,\"robotX\":0,\"robotY\":0}]" }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static TranslationRotationCalibrationOperator CreateSut()
    {
        return new TranslationRotationCalibrationOperator(Substitute.For<ILogger<TranslationRotationCalibrationOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("TranslationRotationCalibration", OperatorType.TranslationRotationCalibration, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static string BuildCalibrationPointsJson()
    {
        return "[" +
               "{\"imageX\":0,\"imageY\":0,\"robotX\":10,\"robotY\":20,\"angle\":0}," +
               "{\"imageX\":10,\"imageY\":0,\"robotX\":20,\"robotY\":20,\"angle\":0}," +
               "{\"imageX\":0,\"imageY\":10,\"robotX\":10,\"robotY\":30,\"angle\":0}," +
               "{\"imageX\":20,\"imageY\":10,\"robotX\":30,\"robotY\":30,\"angle\":0}" +
               "]";
    }
}
