// GeometricToleranceOperatorTests.cs
// GeometricToleranceOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
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
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.GeometricTolerance, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.GeometricTolerance, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ExecuteAsync_WithParallelLines_ShouldReportAngleOnlyModel()
    {
        var op = new Operator("测试", OperatorType.GeometricTolerance, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "Parallelism", "enum"));
        op.AddParameter(TestHelpers.CreateParameter("Line1_X1", 10, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line1_Y1", 30, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line1_X2", 180, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line1_Y2", 30, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line2_X1", 20, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line2_Y1", 80, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line2_X2", 170, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Line2_Y2", 80, "int"));

        using var image = TestHelpers.CreateTestImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("MeasurementModel");
        result.OutputData!["MeasurementModel"].Should().Be("AngleOnly");
        result.OutputData.Should().ContainKey("AngularDeviationDeg");
        result.OutputData.Should().ContainKey("LinearBand");

        Convert.ToDouble(result.OutputData["Tolerance"]).Should().BeApproximately(0.0, 1e-6);
        Convert.ToDouble(result.OutputData["AngularDeviationDeg"]).Should().BeApproximately(0.0, 1e-6);
        Convert.ToDouble(result.OutputData["LinearBand"]).Should().BeApproximately(0.0, 1e-6);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.GeometricTolerance, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
