using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class GeometricToleranceOperatorTests
{
    private readonly GeometricToleranceOperator _operator;

    public GeometricToleranceOperatorTests()
    {
        _operator = new GeometricToleranceOperator(Substitute.For<ILogger<GeometricToleranceOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeGeometricTolerance()
    {
        _operator.OperatorType.Should().Be(OperatorType.GeometricTolerance);
    }

    [Fact]
    public async Task ExecuteAsync_Parallelism_ShouldReturnDatumZoneDecision()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Parallelism", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 1.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new LineData(10, 30, 180, 30),
            ["DatumA"] = new LineData(20, 80, 170, 80)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["MeasurementModel"].Should().Be("DatumZone2D");
        result.OutputData["Accepted"].Should().Be(true);
        Convert.ToDouble(result.OutputData["ZoneDeviation"]).Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_Position_ShouldUseDatumFrameAndNominalTarget()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "CircularZone", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Position(10, 5),
            ["DatumA"] = new LineData(0, 0, 20, 0),
            ["DatumB"] = new LineData(0, 0, 0, 20)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["Accepted"].Should().Be(true);
        Convert.ToDouble(result.OutputData["ZoneDeviation"]).Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_Position_WithNonOrthogonalDatums_ShouldUseOrthogonalizedFrame()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "CircularZone", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Position(10, 5),
            ["DatumA"] = new LineData(0, 0, 20, 0),
            ["DatumB"] = new LineData(0, 0, 20, 20)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["Accepted"].Should().Be(true);
        Convert.ToDouble(result.OutputData["ZoneDeviation"]).Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_Parallelism_WithDegenerateLine_ShouldFail()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Parallelism", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 1.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new LineData(10, 10, 10, 10),
            ["DatumA"] = new LineData(20, 80, 170, 80)
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("degenerate");
    }

    [Fact]
    public void ValidateParameters_WithInvalidToleranceType_ShouldReturnInvalid()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Runout", "string"));
        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
