using System.Linq;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
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

        var calibrationData = Assert.IsType<string>(result.OutputData!["CalibrationData"]);
        Assert.True(CalibrationBundleV2Json.TryDeserialize(calibrationData, out var bundle, out var error), error);
        Assert.NotNull(bundle.Transform2D);
        Assert.True(bundle.Transform2D!.Matrix.All(row => row.Length == 3));
        Assert.Equal("Similarity", result.OutputData["TransformModel"]);
        Assert.True(result.OutputData.ContainsKey("Accepted"));
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
