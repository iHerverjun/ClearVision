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
    public async Task ExecuteAsync_Parallelism_WithSubpixelDeviation_ShouldReturnAnalyticZoneDeviation()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Parallelism", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 0.10, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Dictionary<string, object>
            {
                ["StartX"] = 10.25,
                ["StartY"] = 30.10,
                ["EndX"] = 180.25,
                ["EndY"] = 30.18,
                ["UncertaintyPx"] = 0.05
            },
            ["DatumA"] = new Dictionary<string, object>
            {
                ["StartX"] = 20.0,
                ["StartY"] = 80.0,
                ["EndX"] = 170.0,
                ["EndY"] = 80.0,
                ["UncertaintyPx"] = 0.05
            }
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["Accepted"].Should().Be(true);
        Convert.ToDouble(result.OutputData["ZoneDeviation"]).Should().BeApproximately(0.08, 1e-6);
        Convert.ToDouble(result.OutputData["ToleranceMargin"]).Should().BeApproximately(0.02, 1e-6);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeGreaterThan(0.0).And.BeLessThan(0.2);
    }

    [Fact]
    public async Task ExecuteAsync_Position_WithSubpixelDatumFrame_ShouldReturnAnalyticDeviation()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 0.02, "double"));
        op.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "CircularZone", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Dictionary<string, object>
            {
                ["X"] = 10.003,
                ["Y"] = 5.004,
                ["UncertaintyPx"] = 0.05
            },
            ["DatumA"] = new Dictionary<string, object>
            {
                ["StartX"] = 0.0,
                ["StartY"] = 0.0,
                ["EndX"] = 20.0,
                ["EndY"] = 0.0,
                ["UncertaintyPx"] = 0.05
            },
            ["DatumB"] = new Dictionary<string, object>
            {
                ["StartX"] = 0.0,
                ["StartY"] = 0.0,
                ["EndX"] = 0.0,
                ["EndY"] = 20.0,
                ["UncertaintyPx"] = 0.05
            }
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["Accepted"].Should().Be(true);
        Convert.ToDouble(result.OutputData["ZoneDeviation"]).Should().BeApproximately(0.005, 1e-6);
        Convert.ToDouble(result.OutputData["ToleranceMargin"]).Should().BeApproximately(0.005, 1e-6);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeGreaterThan(0.0);
        Convert.ToDouble(result.OutputData["Confidence"]).Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task ExecuteAsync_Position_Projected2D_ShouldUseAdditiveProjectedDeviation_NotCircularRadius()
    {
        var circularOp = new Operator("gtol-circ", OperatorType.GeometricTolerance, 0, 0);
        circularOp.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        circularOp.AddParameter(TestHelpers.CreateParameter("ZoneSize", 2.0, "double"));
        circularOp.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "CircularZone", "string"));
        circularOp.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        circularOp.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var projectedOp = new Operator("gtol-proj", OperatorType.GeometricTolerance, 0, 0);
        projectedOp.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Position", "string"));
        projectedOp.AddParameter(TestHelpers.CreateParameter("ZoneSize", 2.0, "double"));
        projectedOp.AddParameter(TestHelpers.CreateParameter("EvaluationMode", "Projected2D", "string"));
        projectedOp.AddParameter(TestHelpers.CreateParameter("NominalX", 10.0, "double"));
        projectedOp.AddParameter(TestHelpers.CreateParameter("NominalY", 5.0, "double"));

        var inputs = new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Position(10.6, 5.6),
            ["DatumA"] = new LineData(0, 0, 20, 0),
            ["DatumB"] = new LineData(0, 0, 0, 20)
        };

        var circularResult = await _operator.ExecuteAsync(circularOp, inputs);
        var projectedResult = await _operator.ExecuteAsync(projectedOp, inputs);

        circularResult.IsSuccess.Should().BeTrue(circularResult.ErrorMessage);
        projectedResult.IsSuccess.Should().BeTrue(projectedResult.ErrorMessage);

        Convert.ToDouble(circularResult.OutputData!["ZoneDeviation"]).Should().BeApproximately(Math.Sqrt(0.72), 1e-6);
        Convert.ToDouble(projectedResult.OutputData!["ZoneDeviation"]).Should().BeApproximately(1.2, 1e-6);
        circularResult.OutputData!["Accepted"].Should().Be(true);
        projectedResult.OutputData!["Accepted"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_Parallelism_WithInvalidUncertaintyPx_ShouldIgnoreInvalidExternalValues()
    {
        var op = new Operator("gtol", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ToleranceType", "Parallelism", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ZoneSize", 0.10, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["FeaturePrimary"] = new Dictionary<string, object>
            {
                ["StartX"] = 10.25,
                ["StartY"] = 30.10,
                ["EndX"] = 180.25,
                ["EndY"] = 30.18,
                ["UncertaintyPx"] = 0.0
            },
            ["DatumA"] = new Dictionary<string, object>
            {
                ["StartX"] = 20.0,
                ["StartY"] = 80.0,
                ["EndX"] = 170.0,
                ["EndY"] = 80.0,
                ["UncertaintyPx"] = -0.1
            }
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["UncertaintyPx"]).Should().BeGreaterThan(0.0);
        Convert.ToDouble(result.OutputData["Confidence"]).Should().BeLessThan(1.0);
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
