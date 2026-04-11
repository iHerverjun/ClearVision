using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MeasureDistanceOperatorTests
{
    private readonly MeasureDistanceOperator _operator;

    public MeasureDistanceOperatorTests()
    {
        _operator = new MeasureDistanceOperator(Substitute.For<ILogger<MeasureDistanceOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeMeasurement()
    {
        _operator.OperatorType.Should().Be(OperatorType.Measurement);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.Measurement, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.Measurement, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
        result.OutputData.Should().ContainKey("StatusCode");
        result.OutputData!["StatusCode"].Should().Be("OK");
    }

    [Fact]
    public async Task ExecuteAsync_WithPointInputs_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.Measurement, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            { "PointA", new Point(10, 10) },
            { "PointB", new Point(25, 10) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Distance");
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(15.0, 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_WithDegeneratePointInput_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.Measurement, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            { "PointA", new Point(10, 10) },
            { "PointB", new Point(10, 10) }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DegenerateGeometry");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.Measurement, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
