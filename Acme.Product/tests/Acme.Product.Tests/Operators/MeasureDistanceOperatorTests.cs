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
    public async Task ExecuteAsync_PointInputsShouldRespectHorizontalMeasureType()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MeasureType", "Horizontal", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PointA"] = new Point(10, 10),
            ["PointB"] = new Point(25, 30)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToDouble(result.OutputData!["Distance"]).Should().BeApproximately(15.0, 1e-6);
        Convert.ToInt32(result.OutputData["Y2"]).Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("measure", OperatorType.Measurement, 0, 0);
        (await _operator.ExecuteAsync(op, null)).IsSuccess.Should().BeFalse();
    }
}
