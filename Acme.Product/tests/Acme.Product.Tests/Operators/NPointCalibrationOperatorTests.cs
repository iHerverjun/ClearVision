using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class NPointCalibrationOperatorTests
{
    private readonly NPointCalibrationOperator _operator;

    public NPointCalibrationOperatorTests()
    {
        _operator = new NPointCalibrationOperator(Substitute.For<ILogger<NPointCalibrationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeNPointCalibration()
    {
        Assert.Equal(OperatorType.NPointCalibration, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithAffinePairs_ShouldReturnMatrix()
    {
        var pairsJson = "[" +
                        "{\"ImageX\":0,\"ImageY\":0,\"WorldX\":0,\"WorldY\":0}," +
                        "{\"ImageX\":10,\"ImageY\":0,\"WorldX\":20,\"WorldY\":0}," +
                        "{\"ImageX\":0,\"ImageY\":10,\"WorldX\":0,\"WorldY\":20}" +
                        "]";

        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CalibrationMode", "Affine" },
            { "PointPairs", pairsJson }
        });

        var result = await _operator.ExecuteAsync(op, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        var calibrationData = Assert.IsType<string>(result.OutputData!["CalibrationData"]);
        Assert.True(CalibrationBundleV2Json.TryDeserialize(calibrationData, out var bundle, out var error), error);
        Assert.NotNull(bundle.Transform2D);
    }

    [Fact]
    public async Task ExecuteAsync_WithInsufficientPerspectivePairs_ShouldReturnFailure()
    {
        var pairsJson = "[" +
                        "{\"ImageX\":0,\"ImageY\":0,\"WorldX\":0,\"WorldY\":0}," +
                        "{\"ImageX\":10,\"ImageY\":0,\"WorldX\":20,\"WorldY\":0}," +
                        "{\"ImageX\":0,\"ImageY\":10,\"WorldX\":0,\"WorldY\":20}" +
                        "]";

        var op = CreateOperator(new Dictionary<string, object>
        {
            { "CalibrationMode", "Perspective" },
            { "PointPairs", pairsJson }
        });

        var result = await _operator.ExecuteAsync(op, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "CalibrationMode", "Unknown" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("NPoint", OperatorType.NPointCalibration, 0, 0);

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
