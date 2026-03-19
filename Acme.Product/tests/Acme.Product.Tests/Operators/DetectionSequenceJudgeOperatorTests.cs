using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class DetectionSequenceJudgeOperatorTests
{
    private readonly DetectionSequenceJudgeOperator _sut =
        new(Substitute.For<ILogger<DetectionSequenceJudgeOperator>>());

    [Fact]
    public async Task ExecuteAsync_WithExpectedOrder_ShouldReturnMatch()
    {
        var op = CreateOperator(expectedLabels: "Wire_Brown,Wire_Black,Wire_Blue");
        var detections = CreateDetections(
            ("Wire_Black", 0.96f, 30f),
            ("Wire_Blue", 0.94f, 50f),
            ("Wire_Brown", 0.98f, 10f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["Count"].Should().Be(3);
        result.OutputData["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" });
        result.OutputData["MissingLabels"].Should().BeEquivalentTo(Array.Empty<string>());
        result.OutputData["DuplicateLabels"].Should().BeEquivalentTo(Array.Empty<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongOrder_ShouldReturnOrderMismatch()
    {
        var op = CreateOperator(expectedLabels: "Wire_Brown,Wire_Black,Wire_Blue");
        var detections = CreateDetections(
            ("Wire_Brown", 0.98f, 10f),
            ("Wire_Blue", 0.94f, 30f),
            ("Wire_Black", 0.96f, 50f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_Brown", "Wire_Blue", "Wire_Black" });
        result.OutputData["Message"].Should().BeOfType<string>()
            .Which.Should().Contain("Order mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingLabel_ShouldReturnMissingLabels()
    {
        var op = CreateOperator(expectedLabels: "Wire_Brown,Wire_Black,Wire_Blue");
        var detections = CreateDetections(
            ("Wire_Brown", 0.98f, 10f),
            ("Wire_Blue", 0.94f, 30f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["MissingLabels"].Should().BeEquivalentTo(new[] { "Wire_Black" });
        result.OutputData["Message"].Should().BeOfType<string>()
            .Which.Should().Contain("Missing labels");
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateLabel_ShouldReturnDuplicateLabels()
    {
        var op = CreateOperator(expectedLabels: "Wire_Brown,Wire_Black,Wire_Blue");
        var detections = CreateDetections(
            ("Wire_Brown", 0.98f, 10f),
            ("Wire_Black", 0.95f, 30f),
            ("Wire_Black", 0.91f, 50f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["DuplicateLabels"].Should().BeEquivalentTo(new[] { "Wire_Black" });
        result.OutputData["Message"].Should().BeOfType<string>()
            .Which.Should().Contain("Duplicate labels");
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidenceFilter_ShouldFailWhenCountDropsBelowExpectation()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_Brown,Wire_Black,Wire_Blue",
            minConfidence: 0.9);
        var detections = CreateDetections(
            ("Wire_Brown", 0.98f, 10f),
            ("Wire_Black", 0.95f, 30f),
            ("Wire_Blue", 0.42f, 50f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["Count"].Should().Be(2);
        result.OutputData["MissingLabels"].Should().BeEquivalentTo(new[] { "Wire_Blue" });
        result.OutputData["Message"].Should().BeOfType<string>()
            .Which.Should().Contain("Expected 3 detections but got 2");
    }

    private static Operator CreateOperator(
        string expectedLabels,
        double minConfidence = 0.0,
        string sortBy = "CenterX",
        string direction = "Ascending")
    {
        var op = new Operator("judge", OperatorType.DetectionSequenceJudge, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ExpectedLabels", expectedLabels, "string"));
        op.AddParameter(TestHelpers.CreateParameter("SortBy", sortBy, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Direction", direction, "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectedCount", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MinConfidence", minConfidence, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AllowMissing", false, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("AllowDuplicate", false, "bool"));
        return op;
    }

    private static IEnumerable<DetectionResult> CreateDetections(params (string Label, float Confidence, float X)[] items)
    {
        return items.Select(item => new DetectionResult(
            item.Label,
            item.Confidence,
            item.X,
            10f,
            8f,
            8f));
    }
}
