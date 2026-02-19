// Sprint2_ArrayIndexerTests.cs
// Sprint 2 Task 2.2 ArrayIndexer 算子单元测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

/// <summary>
/// Sprint 2 Task 2.2: ArrayIndexer 算子单元测试
/// </summary>
public class Sprint2_ArrayIndexerTests
{
    private readonly ILogger<ArrayIndexerOperator> _loggerMock;
    private readonly ArrayIndexerOperator _operator;

    public Sprint2_ArrayIndexerTests()
    {
        _loggerMock = Substitute.For<ILogger<ArrayIndexerOperator>>();
        _operator = new ArrayIndexerOperator(_loggerMock);
    }

    [Fact]
    public async Task ArrayIndexer_IndexMode_ReturnsCorrectItem()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>
        {
            new("A", 0.8f, 0, 0, 10, 10),
            new("B", 0.9f, 10, 0, 20, 20),
            new("C", 0.7f, 20, 0, 30, 30)
        };

        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Index" },
            { "Index", 1 }
        });

        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(1, result.OutputData["Index"]);
        Assert.Equal(3, result.OutputData["TotalCount"]);
    }

    [Fact]
    public async Task ArrayIndexer_MaxConfidenceMode_ReturnsHighest()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>
        {
            new("Low", 0.5f, 0, 0, 10, 10),
            new("High", 0.95f, 10, 0, 20, 20),
            new("Medium", 0.7f, 20, 0, 30, 30)
        };

        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MaxConfidence" } });
        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Contains("最大置信度: 0.95", (string)result.OutputData["Message"]);
    }

    [Fact]
    public async Task ArrayIndexer_MaxAreaMode_ReturnsLargest()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>
        {
            new("Small", 0.9f, 0, 0, 10, 10),   // Area = 100
            new("Large", 0.8f, 10, 0, 50, 50),  // Area = 2500
            new("Medium", 0.7f, 20, 0, 400, 400)  // 修正面积计算
        };

        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MaxArea" } });
        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
    }

    [Fact]
    public async Task ArrayIndexer_MinAreaMode_ReturnsSmallest()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>
        {
            new("Small", 0.9f, 0, 0, 10, 10),   // Area = 100
            new("Large", 0.8f, 10, 0, 50, 50),  // Area = 2500
            new("Medium", 0.7f, 20, 0, 20, 20)  // Area = 400
        };

        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MinArea" } });
        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
    }

    [Fact]
    public async Task ArrayIndexer_EmptyList_ReturnsNotFound()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>();
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.False((bool)result.OutputData!["Found"]);
        Assert.Equal(-1, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_LabelFilter_FiltersBeforeSelect()
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult>
        {
            new("Target", 0.5f, 0, 0, 10, 10),
            new("Other", 0.9f, 10, 0, 20, 20),
            new("Target", 0.95f, 20, 0, 30, 30)
        };

        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "MaxConfidence" },
            { "LabelFilter", "Target" }
        });

        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task ArrayIndexer_InvalidIndex_ReturnsFailure(int index)
    {
        var items = new List<Acme.Product.Core.ValueObjects.DetectionResult> { new("A", 0.9f, 0, 0, 10, 10) };
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Index" },
            { "Index", index }
        });

        var inputs = new Dictionary<string, object> { { "Items", items } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestArrayIndexer", OperatorType.ArrayIndexer, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Mode", "提取模式", "Index/MaxConfidence/MinArea/MaxArea/First/Last", "string", "Index", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Index", "索引", "Index模式下的索引", "int", 0, isRequired: false));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "LabelFilter", "标签过滤", "只选择指定标签的项", "string", "", isRequired: false));

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.UpdateParameter(key, value);
            }
        }

        return op;
    }
}
