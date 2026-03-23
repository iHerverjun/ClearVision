using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class BoxNmsOperatorTests
{
    private readonly BoxNmsOperator _operator;

    public BoxNmsOperatorTests()
    {
        _operator = new BoxNmsOperator(Substitute.For<ILogger<BoxNmsOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeBoxNms()
    {
        Assert.Equal(OperatorType.BoxNms, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithOverlappingBoxes_ShouldReduceCount()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "IouThreshold", 0.5 },
            { "ScoreThreshold", 0.1 },
            { "MaxDetections", 10 }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("defect", 0.95f, 10, 10, 40, 40),
            new DetectionResult("defect", 0.85f, 12, 12, 40, 40),
            new DetectionResult("defect", 0.75f, 80, 80, 30, 30)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(2, (int)result.OutputData!["Count"]);
        result.OutputData["InputCount"].Should().Be(3);
        result.OutputData["SuppressedCount"].Should().Be(1);
        result.OutputData["SuppressedDetections"].Should().BeOfType<DetectionList>();
        result.OutputData["Diagnostics"].Should().BeAssignableTo<Dictionary<string, object>>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxDetectionsTrimsResults_ShouldCountTrimmedBoxesAsSuppressed()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "IouThreshold", 0.5 },
            { "ScoreThreshold", 0.1 },
            { "MaxDetections", 2 }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("wire", 0.99f, 10, 10, 10, 10),
            new DetectionResult("wire", 0.89f, 40, 10, 10, 10),
            new DetectionResult("wire", 0.79f, 70, 10, 10, 10)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Count"].Should().Be(2);
        result.OutputData["SuppressedCount"].Should().Be(1);
        var suppressedDetections = result.OutputData["SuppressedDetections"].Should().BeOfType<DetectionList>().Subject;
        suppressedDetections.Count.Should().Be(1);
        suppressedDetections.Detections.Single().Confidence.Should().Be(0.79f);

        var diagnostics = result.OutputData["Diagnostics"].Should().BeAssignableTo<Dictionary<string, object>>().Subject;
        diagnostics["SuppressedCount"].Should().Be(1);
    }

    [Fact]
    public void ValidateParameters_WithInvalidIou_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "IouThreshold", 0.01 } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("BoxNms", OperatorType.BoxNms, 0, 0);

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
