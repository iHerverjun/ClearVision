using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class Step123_GapAnalysisImplementationTests
{
    [Fact]
    public void OperatorFactory_ShouldExposeKeywordsAndNewMetadataPorts()
    {
        IOperatorFactory factory = new OperatorFactory();

        var all = factory.GetAllMetadata().ToList();
        var withKeywords = all.Where(m => m.Keywords is { Length: > 0 }).ToList();
        Assert.True(withKeywords.Count >= 25, $"P1 requires at least 25 operators with keywords, found {withKeywords.Count}.");

        var math = factory.GetMetadata(OperatorType.MathOperation)!;
        Assert.Contains(math.OutputPorts, p => p.Name == "IsPositive" && p.DataType == PortDataType.Boolean);

        // P1: Measurement should support PointA/PointB
        var measurement = factory.GetMetadata(OperatorType.Measurement)!;
        Assert.Contains(measurement.InputPorts, p => p.Name == "PointA" && p.DataType == PortDataType.Point);
        Assert.Contains(measurement.InputPorts, p => p.Name == "PointB" && p.DataType == PortDataType.Point);

        // P1: Glue operators should have metadata
        Assert.NotNull(factory.GetMetadata(OperatorType.Comparator));
        Assert.NotNull(factory.GetMetadata(OperatorType.Aggregator));
        Assert.NotNull(factory.GetMetadata(OperatorType.Delay));
        Assert.NotNull(factory.GetMetadata(OperatorType.Comment));
    }

    [Fact(Skip = "Pending P1 Implementation")]
    public async Task ComparatorOperator_ShouldCompareValues()
    {
        var logger = Substitute.For<ILogger<ComparatorOperator>>();
        var opExecutor = new ComparatorOperator(logger);
        var op = new Operator(Guid.NewGuid(), "cmp", OperatorType.Comparator, 0, 0);

        op.AddParameter(new Parameter(Guid.NewGuid(), "Condition", "Condition", "", "string", "GreaterThan"));

        var result = await opExecutor.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["ValueA"] = 8,
            ["ValueB"] = 3
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(true, result.OutputData!["Result"]);
        Assert.Equal(5d, result.OutputData!["Difference"]);
    }

    [Fact(Skip = "Pending P1 Implementation")]
    public async Task MeasurementOperator_ShouldSupportPointInputsWithoutImage()
    {
        var logger = Substitute.For<ILogger<MeasureDistanceOperator>>();
        var opExecutor = new MeasureDistanceOperator(logger);
        var op = new Operator(Guid.NewGuid(), "measure", OperatorType.Measurement, 0, 0);

        op.AddParameter(new Parameter(Guid.NewGuid(), "MeasureType", "MeasureType", "", "string", "PointToPoint"));

        var result = await opExecutor.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["PointA"] = "(0,0)",
            ["PointB"] = "(3,4)"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(5d, result.OutputData!["Distance"]);
    }
}
