using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

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
        var diagnostics = result.OutputData["Diagnostics"].Should().BeAssignableTo<Dictionary<string, object>>().Subject;
        diagnostics["ReceivedCount"].Should().Be(3);
        diagnostics["FilteredCount"].Should().Be(3);
        diagnostics["SortedCount"].Should().Be(3);
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
        var diagnostics = result.OutputData["Diagnostics"].Should().BeAssignableTo<Dictionary<string, object>>().Subject;
        diagnostics["ReceivedCount"].Should().Be(3);
        diagnostics["FilteredCount"].Should().Be(2);
        diagnostics["MinConfidence"].Should().Be(0.9);
    }

    [Fact]
    public async Task ExecuteAsync_WithTopYSort_ShouldRespectTopEdgeInsteadOfCenterY()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_Black,Wire_Blue",
            sortBy: "TopY",
            direction: "TopToBottom");
        var detections = CreateDetections(
            ("Wire_Black", 0.96f, 20f, 10f, 10f, 60f),
            ("Wire_Blue", 0.94f, 22f, 18f, 10f, 8f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_Black", "Wire_Blue" });
    }

    [Fact]
    public async Task ExecuteAsync_WithTopYSort_ShouldUseCenterXAsTieBreaker()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_Blue,Wire_Black",
            sortBy: "TopY",
            direction: "TopToBottom");
        var detections = CreateDetections(
            ("Wire_Black", 0.96f, 0f, 10f, 100f, 12f),
            ("Wire_Blue", 0.94f, 10f, 10f, 10f, 20f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_Blue", "Wire_Black" });
        result.OutputData["IsMatch"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithRowCluster_ShouldOrderRowsBeforeColumns()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_TL,Wire_TR,Wire_BL,Wire_BR",
            groupingMode: "RowCluster",
            direction: "LeftToRight",
            rowTolerance: 10.0);
        var detections = CreateDetections(
            ("Wire_TL", 0.98f, 10f, 10f, 8f, 8f),
            ("Wire_BL", 0.96f, 12f, 40f, 8f, 8f),
            ("Wire_TR", 0.97f, 42f, 12f, 8f, 8f),
            ("Wire_BR", 0.95f, 44f, 42f, 8f, 8f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_TL", "Wire_TR", "Wire_BL", "Wire_BR" });
        result.OutputData["RowCount"].Should().Be(2);
        var diagnostics = result.OutputData["Diagnostics"].Should().BeAssignableTo<Dictionary<string, object>>().Subject;
        diagnostics["GroupingModeResolved"].Should().Be("RowCluster");
    }

    [Fact]
    public async Task ExecuteAsync_WithSlotAssignment_ShouldUseSlotLayoutOrder()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_A,Wire_B,Wire_C,Wire_D",
            groupingMode: "SlotAssignment",
            direction: "LeftToRight",
            expectedSlots: "10:10;30:10;10:30;30:30",
            rowTolerance: 8.0,
            slotTolerance: 12.0);
        var detections = CreateDetections(
            ("Wire_A", 0.98f, 10f, 10f, 8f, 8f),
            ("Wire_C", 0.96f, 10f, 30f, 8f, 8f),
            ("Wire_B", 0.97f, 30f, 10f, 8f, 8f),
            ("Wire_D", 0.95f, 30f, 30f, 8f, 8f));

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_A", "Wire_B", "Wire_C", "Wire_D" });
        result.OutputData["IsMatch"].Should().Be(true);
        result.OutputData["Assignment"].Should().BeAssignableTo<List<Dictionary<string, object>>>().Which.Should().HaveCount(4);
    }

    [Fact]
    public async Task ExecuteAsync_WithPerspectivePoints_ShouldApplyRectifiedOrdering()
    {
        var op = CreateOperator(
            expectedLabels: "Wire_1,Wire_2,Wire_3",
            groupingMode: "SlotAssignment",
            direction: "LeftToRight",
            expectedSlots: "20:50;50:50;80:50",
            rowTolerance: 10.0,
            slotTolerance: 12.0);
        op.AddParameter(TestHelpers.CreateParameter("PerspectiveSrcPointsJson", "[[0,0],[100,0],[90,100],[10,100]]", "string"));
        op.AddParameter(TestHelpers.CreateParameter("PerspectiveDstPointsJson", "[[0,0],[100,0],[100,100],[0,100]]", "string"));

        using var inverseTransform = Cv2.GetPerspectiveTransform(
            new[] { new Point2f(0, 0), new Point2f(100, 0), new Point2f(100, 100), new Point2f(0, 100) },
            new[] { new Point2f(0, 0), new Point2f(100, 0), new Point2f(90, 100), new Point2f(10, 100) });

        var detections = new[]
        {
            CreateDetection("Wire_2", TransformPoint(inverseTransform, 50, 50)),
            CreateDetection("Wire_3", TransformPoint(inverseTransform, 80, 50)),
            CreateDetection("Wire_1", TransformPoint(inverseTransform, 20, 50))
        };

        var result = await _sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Detections"] = new DetectionList(detections)
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        result.OutputData!["ActualOrder"].Should().BeEquivalentTo(new[] { "Wire_1", "Wire_2", "Wire_3" });
        result.OutputData["PerspectiveApplied"].Should().Be(true);
    }

    private static Operator CreateOperator(
        string expectedLabels,
        double minConfidence = 0.0,
        string sortBy = "CenterX",
        string direction = "Ascending",
        string groupingMode = "SingleRow",
        string expectedSlots = "",
        double rowTolerance = 0.0,
        double slotTolerance = 0.0)
    {
        var op = new Operator("judge", OperatorType.DetectionSequenceJudge, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ExpectedLabels", expectedLabels, "string"));
        op.AddParameter(TestHelpers.CreateParameter("SortBy", sortBy, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Direction", direction, "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectedCount", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("MinConfidence", minConfidence, "double"));
        op.AddParameter(TestHelpers.CreateParameter("AllowMissing", false, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("AllowDuplicate", false, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("GroupingMode", groupingMode, "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectedSlots", expectedSlots, "string"));
        op.AddParameter(TestHelpers.CreateParameter("RowTolerance", rowTolerance, "double"));
        op.AddParameter(TestHelpers.CreateParameter("SlotTolerance", slotTolerance, "double"));
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

    private static IEnumerable<DetectionResult> CreateDetections(params (string Label, float Confidence, float X, float Y, float Width, float Height)[] items)
    {
        return items.Select(item => new DetectionResult(
            item.Label,
            item.Confidence,
            item.X,
            item.Y,
            item.Width,
            item.Height));
    }

    private static DetectionResult CreateDetection(string label, Point2f center)
    {
        return new DetectionResult(label, 0.95f, center.X - 4, center.Y - 4, 8, 8);
    }

    private static Point2f TransformPoint(Mat transform, float x, float y)
    {
        var m00 = transform.At<double>(0, 0);
        var m01 = transform.At<double>(0, 1);
        var m02 = transform.At<double>(0, 2);
        var m10 = transform.At<double>(1, 0);
        var m11 = transform.At<double>(1, 1);
        var m12 = transform.At<double>(1, 2);
        var m20 = transform.At<double>(2, 0);
        var m21 = transform.At<double>(2, 1);
        var m22 = transform.At<double>(2, 2);
        var denominator = (m20 * x) + (m21 * y) + m22;
        return new Point2f(
            (float)(((m00 * x) + (m01 * y) + m02) / denominator),
            (float)(((m10 * x) + (m11 * y) + m12) / denominator));
    }
}
