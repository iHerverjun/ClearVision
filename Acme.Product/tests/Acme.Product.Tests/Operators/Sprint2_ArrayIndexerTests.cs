using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

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
        var items = BuildSampleItems();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Index" },
            { "Index", 1 }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(1, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_MaxConfidenceMode_ReturnsHighestAndOriginalIndex()
    {
        var items = BuildSampleItems();
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MaxConfidence" } });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(1, result.OutputData["Index"]);
        Assert.Contains("最大置信度", (string)result.OutputData["Message"]);
    }

    [Fact]
    public async Task ArrayIndexer_MaxAreaMode_ReturnsLargestAndOriginalIndex()
    {
        var items = new List<DetectionResult>
        {
            new("Small", 0.9f, 0, 0, 10, 10),
            new("Large", 0.8f, 10, 0, 50, 50),
            new("Largest", 0.7f, 20, 0, 60, 60)
        };

        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MaxArea" } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(2, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_LabelFilter_MaxConfidence_ReturnsOriginalIndex()
    {
        var items = new List<DetectionResult>
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

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(2, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_LabelFilter_IndexMode_ReturnsOriginalIndex()
    {
        var items = new List<DetectionResult>
        {
            new("Target", 0.5f, 0, 0, 10, 10),
            new("Other", 0.9f, 10, 0, 20, 20),
            new("Target", 0.95f, 20, 0, 30, 30)
        };
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Index" },
            { "Index", 1 },
            { "LabelFilter", "Target" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(2, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_EmptyList_ReturnsNotFound()
    {
        var op = CreateOperator();
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", new List<DetectionResult>() } });

        Assert.True(result.IsSuccess);
        Assert.False((bool)result.OutputData!["Found"]);
        Assert.Equal(-1, result.OutputData["Index"]);
    }

    [Fact]
    public async Task ArrayIndexer_ItemsInputOnly_ReturnsFailure()
    {
        var items = BuildSampleItems();
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Items", items } };

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ArrayIndexer_MaxConfidence_WithNonDetectionResultItems_ShouldFail()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Mode", "MaxConfidence" } });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "List", new List<object> { "A", "B" } }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("DetectionResult", result.ErrorMessage ?? string.Empty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task ArrayIndexer_InvalidIndex_ReturnsFailure(int index)
    {
        var items = BuildSampleItems();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Mode", "Index" },
            { "Index", index }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "List", items } });

        Assert.False(result.IsSuccess);
    }

    private static List<DetectionResult> BuildSampleItems()
    {
        return
        [
            new("A", 0.8f, 0, 0, 10, 10),
            new("B", 0.95f, 10, 0, 20, 20),
            new("C", 0.7f, 20, 0, 30, 30)
        ];
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestArrayIndexer", OperatorType.ArrayIndexer, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Mode", "提取模式", "Index/MaxConfidence/MinArea/MaxArea/First/Last", "string", "Index", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Index", "索引", "Index 模式下的索引", "int", 0, isRequired: false));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "LabelFilter", "标签过滤", "只选择指定标签项", "string", string.Empty, isRequired: false));

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
