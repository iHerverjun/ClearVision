using System.Collections;
using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Detection Sequence Judge",
    Description = "Sorts detections and compares the resulting label order against an expected sequence.",
    Category = "AI Inspection",
    IconName = "rule",
    Keywords = new[]
    {
        "sequence",
        "order",
        "wire",
        "wiring",
        "terminal",
        "connector",
        "harness",
        "线序",
        "顺序",
        "接线",
        "端子",
        "line-sequence",
        "judge"
    },
    Tags = new[] { "experimental", "industrial-remediation", "sequence-judge" })]
[InputPort("Detections", "Detections", PortDataType.DetectionList, IsRequired = true)]
[InputPort("SlotPoints", "Slot Points", PortDataType.PointList, IsRequired = false)]
[InputPort("PerspectiveSrcPoints", "Perspective Source Points", PortDataType.PointList, IsRequired = false)]
[InputPort("PerspectiveDstPoints", "Perspective Destination Points", PortDataType.PointList, IsRequired = false)]
[OutputPort("IsMatch", "Is Match", PortDataType.Boolean)]
[OutputPort("ActualOrder", "Actual Order", PortDataType.Any)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("MissingLabels", "Missing Labels", PortDataType.Any)]
[OutputPort("DuplicateLabels", "Duplicate Labels", PortDataType.Any)]
[OutputPort("SortedDetections", "Sorted Detections", PortDataType.DetectionList)]
[OutputPort("Assignment", "Assignment", PortDataType.Any)]
[OutputPort("UnassignedDetections", "Unassigned Detections", PortDataType.DetectionList)]
[OutputPort("SlotDistances", "Slot Distances", PortDataType.Any)]
[OutputPort("RowCount", "Row Count", PortDataType.Integer)]
[OutputPort("PerspectiveApplied", "Perspective Applied", PortDataType.Boolean)]
[OutputPort("Diagnostics", "Diagnostics", PortDataType.Any)]
[OutputPort("Message", "Message", PortDataType.String)]
[OperatorParam(
    "ExpectedLabels",
    "Expected Labels",
    "string",
    Description = "Comma-separated expected labels in order.",
    DefaultValue = "")]
[OperatorParam(
    "SortBy",
    "Sort By",
    "enum",
    Description = "Field used to sort detections before judging the sequence.",
    DefaultValue = "CenterX",
    Options = new[]
    {
        "CenterX|Center X",
        "CenterY|Center Y",
        "TopY|Top Y",
        "Confidence|Confidence",
        "Area|Area"
    })]
[OperatorParam(
    "Direction",
    "Direction",
    "enum",
    Description = "Ordering direction after sorting.",
    DefaultValue = "Ascending",
    Options = new[]
    {
        "Ascending|Ascending",
        "Descending|Descending",
        "LeftToRight|Left To Right",
        "RightToLeft|Right To Left",
        "TopToBottom|Top To Bottom",
        "BottomToTop|Bottom To Top"
    })]
[OperatorParam(
    "ExpectedCount",
    "Expected Count",
    "int",
    Description = "Expected detection count. Use 0 to derive from ExpectedLabels.",
    DefaultValue = 0,
    Min = 0,
    Max = 256)]
[OperatorParam(
    "MinConfidence",
    "Min Confidence",
    "double",
    Description = "Ignore detections below this confidence before sequence judgment.",
    DefaultValue = 0.0,
    Min = 0.0,
    Max = 1.0)]
[OperatorParam(
    "AllowMissing",
    "Allow Missing",
    "bool",
    Description = "Whether missing expected labels should still be treated as a match.",
    DefaultValue = false)]
[OperatorParam(
    "AllowDuplicate",
    "Allow Duplicate",
    "bool",
    Description = "Whether duplicate labels should still be treated as a match.",
    DefaultValue = false)]
[OperatorParam(
    "GroupingMode",
    "Grouping Mode",
    "enum",
    Description = "SingleRow keeps legacy sorting, RowCluster groups detections into rows, SlotAssignment assigns detections to expected slot points, Auto prefers slots when provided.",
    DefaultValue = "SingleRow",
    Options = new[]
    {
        "SingleRow|Single Row",
        "RowCluster|Row Cluster",
        "SlotAssignment|Slot Assignment",
        "Auto|Auto"
    })]
[OperatorParam(
    "ExpectedSlots",
    "Expected Slots",
    "string",
    Description = "JSON array or shorthand x:y;x:y list of expected slot centers.",
    DefaultValue = "")]
[OperatorParam(
    "RowTolerance",
    "Row Tolerance",
    "double",
    Description = "Maximum Y delta for row clustering. Use 0 for auto.",
    DefaultValue = 0.0,
    Min = 0.0,
    Max = 5000.0)]
[OperatorParam(
    "SlotTolerance",
    "Slot Tolerance",
    "double",
    Description = "Maximum assignment distance to an expected slot. Use 0 for auto.",
    DefaultValue = 0.0,
    Min = 0.0,
    Max = 5000.0)]
[OperatorParam(
    "PerspectiveSrcPointsJson",
    "Perspective Source Points JSON",
    "string",
    Description = "Optional 4-point JSON array for perspective source points.",
    DefaultValue = "")]
[OperatorParam(
    "PerspectiveDstPointsJson",
    "Perspective Destination Points JSON",
    "string",
    Description = "Optional 4-point JSON array for perspective destination points.",
    DefaultValue = "")]
public sealed class DetectionSequenceJudgeOperator : OperatorBase
{
    private static readonly string[] ValidSortBy =
    {
        "CenterX",
        "CenterY",
        "TopY",
        "Confidence",
        "Area"
    };

    private static readonly string[] ValidDirection =
    {
        "Ascending",
        "Descending",
        "LeftToRight",
        "RightToLeft",
        "TopToBottom",
        "BottomToTop"
    };

    private static readonly string[] ValidGroupingModes =
    {
        "SingleRow",
        "RowCluster",
        "SlotAssignment",
        "Auto"
    };

    public override OperatorType OperatorType => OperatorType.DetectionSequenceJudge;

    public DetectionSequenceJudgeOperator(ILogger<DetectionSequenceJudgeOperator> logger)
        : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Detections", out var detectionValue))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Missing Detections input."));
        }

        var expectedLabels = ParseLabels(GetStringParam(@operator, "ExpectedLabels", string.Empty));
        var sortBy = GetStringParam(@operator, "SortBy", "CenterX");
        var direction = GetStringParam(@operator, "Direction", "Ascending");
        var groupingMode = NormalizeGroupingMode(GetStringParam(@operator, "GroupingMode", "SingleRow"));
        var configuredExpectedCount = GetIntParam(@operator, "ExpectedCount", 0, 0, 256);
        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0, 0.0, 1.0);
        var allowMissing = GetBoolParam(@operator, "AllowMissing", false);
        var allowDuplicate = GetBoolParam(@operator, "AllowDuplicate", false);
        var rowTolerance = GetDoubleParam(@operator, "RowTolerance", 0.0, 0.0, 5000.0);
        var slotTolerance = GetDoubleParam(@operator, "SlotTolerance", 0.0, 0.0, 5000.0);

        var rawDetections = DetectionOutputInspector.ExtractDetections(detectionValue);
        var filteredDetections = rawDetections
            .Where(detection => detection.Confidence >= minConfidence)
            .Select((detection, index) => SequenceDetection.Create(CloneDetection(detection), index))
            .ToList();

        if (!TryResolveSlotPoints(inputs, @operator, out var slotPoints, out var slotError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(slotError ?? "Failed to parse slot points."));
        }

        var perspective = ResolvePerspectiveContext(inputs, @operator, out var perspectiveError);
        if (!string.IsNullOrWhiteSpace(perspectiveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(perspectiveError));
        }

        if (perspective.IsValid)
        {
            ApplyPerspective(filteredDetections, perspective);
            ApplyPerspective(slotPoints, perspective);
        }

        var resolvedGroupingMode = ResolveGroupingMode(groupingMode, slotPoints.Count, filteredDetections.Count, rowTolerance);
        var effectiveRowTolerance = ResolveRowTolerance(filteredDetections, rowTolerance);
        var effectiveSlotTolerance = ResolveSlotTolerance(filteredDetections, slotTolerance);

        var orderedDetections = new List<SequenceDetection>();
        var assignment = new List<AssignmentRecord>();
        var unassignedDetections = new List<SequenceDetection>();
        var rowCount = 1;
        string? groupingWarning = null;

        switch (resolvedGroupingMode)
        {
            case "SlotAssignment":
                if (slotPoints.Count == 0)
                {
                    groupingWarning = "GroupingMode resolved to SlotAssignment but no slot points were provided. Falling back to SingleRow.";
                    orderedDetections = SortSequenceDetections(filteredDetections, sortBy, direction);
                }
                else
                {
                    var slotOrder = BuildSlotOrder(slotPoints, direction, effectiveRowTolerance);
                    rowCount = CountPointRows(slotOrder, effectiveRowTolerance);
                    AssignToSlots(filteredDetections, slotOrder, expectedLabels, effectiveSlotTolerance, out assignment, out orderedDetections, out unassignedDetections);
                }
                break;
            case "RowCluster":
                var rowClusters = ClusterRows(filteredDetections, effectiveRowTolerance);
                rowCount = rowClusters.Count;
                orderedDetections = FlattenRows(rowClusters, sortBy, direction);
                break;
            default:
                orderedDetections = SortSequenceDetections(filteredDetections, sortBy, direction);
                break;
        }

        var actualOrder = orderedDetections
            .Select(detection => detection.Detection.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var expectedCount = configuredExpectedCount > 0
            ? configuredExpectedCount
            : expectedLabels.Count > 0
                ? expectedLabels.Count
                : slotPoints.Count;

        var missingLabels = ComputeMissingLabels(expectedLabels, actualOrder);
        var duplicateLabels = actualOrder
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        var reasons = new List<string>();
        if (rawDetections.Count == 0)
        {
            reasons.Add("No detections received.");
        }
        else if (filteredDetections.Count == 0)
        {
            reasons.Add("All detections were filtered by MinConfidence.");
        }

        if (!string.IsNullOrWhiteSpace(groupingWarning))
        {
            reasons.Add(groupingWarning);
        }

        if (!allowMissing && missingLabels.Count > 0)
        {
            reasons.Add($"Missing labels: {string.Join(", ", missingLabels)}.");
        }

        if (!allowDuplicate && duplicateLabels.Count > 0)
        {
            reasons.Add($"Duplicate labels: {string.Join(", ", duplicateLabels)}.");
        }

        if (expectedCount > 0 && orderedDetections.Count != expectedCount)
        {
            reasons.Add($"Expected {expectedCount} detections but got {orderedDetections.Count}.");
        }

        if (expectedLabels.Count > 0 &&
            !expectedLabels.SequenceEqual(actualOrder, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"Order mismatch. Actual: {FormatLabels(actualOrder)}.");
        }

        var isMatch = reasons.Count == 0;
        var message = isMatch
            ? $"Sequence matched: {FormatLabels(actualOrder)}."
            : string.Join(" ", reasons);
        var diagnostics = new Dictionary<string, object>
        {
            ["IsMatch"] = isMatch,
            ["ExpectedLabels"] = expectedLabels,
            ["ActualOrder"] = actualOrder,
            ["ReceivedCount"] = rawDetections.Count,
            ["FilteredCount"] = filteredDetections.Count,
            ["SortedCount"] = orderedDetections.Count,
            ["ExpectedCount"] = expectedCount,
            ["MissingLabels"] = missingLabels,
            ["DuplicateLabels"] = duplicateLabels,
            ["SortBy"] = sortBy,
            ["Direction"] = direction,
            ["GroupingModeRequested"] = groupingMode,
            ["GroupingModeResolved"] = resolvedGroupingMode,
            ["MinConfidence"] = minConfidence,
            ["AllowMissing"] = allowMissing,
            ["AllowDuplicate"] = allowDuplicate,
            ["RowTolerance"] = effectiveRowTolerance,
            ["SlotTolerance"] = effectiveSlotTolerance,
            ["SlotCount"] = slotPoints.Count,
            ["RowCount"] = rowCount,
            ["PerspectiveApplied"] = perspective.IsValid,
            ["PerspectiveSource"] = perspective.Source,
            ["Message"] = message
        };

        var result = new Dictionary<string, object>
        {
            ["IsMatch"] = isMatch,
            ["ActualOrder"] = actualOrder,
            ["Count"] = orderedDetections.Count,
            ["DetectionCount"] = orderedDetections.Count,
            ["MissingLabels"] = missingLabels,
            ["DuplicateLabels"] = duplicateLabels,
            ["SortedDetections"] = new DetectionList(orderedDetections.Select(x => x.Detection)),
            ["Assignment"] = assignment.Select(ToAssignmentDictionary).ToList(),
            ["UnassignedDetections"] = new DetectionList(unassignedDetections.Select(x => x.Detection)),
            ["SlotDistances"] = assignment.Select(x => x.Distance).ToArray(),
            ["ExpectedLabels"] = expectedLabels,
            ["ExpectedCount"] = expectedCount,
            ["RequiredMinConfidence"] = minConfidence,
            ["RowCount"] = rowCount,
            ["PerspectiveApplied"] = perspective.IsValid,
            ["Diagnostics"] = diagnostics,
            ["Message"] = message
        };

        return Task.FromResult(OperatorExecutionOutput.Success(result));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var sortBy = GetStringParam(@operator, "SortBy", "CenterX");
        if (!ValidSortBy.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"SortBy must be one of: {string.Join(", ", ValidSortBy)}");
        }

        var direction = GetStringParam(@operator, "Direction", "Ascending");
        if (!ValidDirection.Contains(direction, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Direction must be one of: {string.Join(", ", ValidDirection)}");
        }

        var groupingMode = GetStringParam(@operator, "GroupingMode", "SingleRow");
        if (!ValidGroupingModes.Contains(groupingMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"GroupingMode must be one of: {string.Join(", ", ValidGroupingModes)}");
        }

        var expectedCount = GetIntParam(@operator, "ExpectedCount", 0, 0, 256);
        if (expectedCount < 0)
        {
            return ValidationResult.Invalid("ExpectedCount must be greater than or equal to 0.");
        }

        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0, 0.0, 1.0);
        if (minConfidence < 0 || minConfidence > 1)
        {
            return ValidationResult.Invalid("MinConfidence must be between 0 and 1.");
        }

        var rowTolerance = GetDoubleParam(@operator, "RowTolerance", 0.0, 0.0, 5000.0);
        if (rowTolerance < 0)
        {
            return ValidationResult.Invalid("RowTolerance must be greater than or equal to 0.");
        }

        var slotTolerance = GetDoubleParam(@operator, "SlotTolerance", 0.0, 0.0, 5000.0);
        if (slotTolerance < 0)
        {
            return ValidationResult.Invalid("SlotTolerance must be greater than or equal to 0.");
        }

        var expectedSlots = GetStringParam(@operator, "ExpectedSlots", string.Empty);
        if (!string.IsNullOrWhiteSpace(expectedSlots) && !TryParsePointCollection(expectedSlots, out _))
        {
            return ValidationResult.Invalid("ExpectedSlots must be a JSON array or shorthand x:y;x:y list.");
        }

        var srcPointsJson = GetStringParam(@operator, "PerspectiveSrcPointsJson", string.Empty);
        var dstPointsJson = GetStringParam(@operator, "PerspectiveDstPointsJson", string.Empty);
        if ((string.IsNullOrWhiteSpace(srcPointsJson) ^ string.IsNullOrWhiteSpace(dstPointsJson)) ||
            (!string.IsNullOrWhiteSpace(srcPointsJson) && (!TryParsePointCollection(srcPointsJson, out var srcPoints) || srcPoints.Count < 4)) ||
            (!string.IsNullOrWhiteSpace(dstPointsJson) && (!TryParsePointCollection(dstPointsJson, out var dstPoints) || dstPoints.Count < 4)))
        {
            return ValidationResult.Invalid("Perspective source/destination points must be provided in pairs and contain at least 4 points.");
        }

        return ValidationResult.Valid();
    }

    private static List<SequenceDetection> SortSequenceDetections(IEnumerable<SequenceDetection> detections, string sortBy, string direction)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy, direction);
        var descending = IsDescending(direction);
        var ordered = normalizedSortBy switch
        {
            "TopY" => descending
                ? detections.OrderByDescending(GetTopY).ThenBy(detection => detection.EvalX)
                : detections.OrderBy(GetTopY).ThenBy(detection => detection.EvalX),
            "CenterY" => descending
                ? detections.OrderByDescending(detection => detection.EvalY).ThenBy(detection => detection.EvalX)
                : detections.OrderBy(detection => detection.EvalY).ThenBy(detection => detection.EvalX),
            "Confidence" => descending
                ? detections.OrderByDescending(detection => detection.Detection.Confidence).ThenBy(detection => detection.EvalX)
                : detections.OrderBy(detection => detection.Detection.Confidence).ThenBy(detection => detection.EvalX),
            "Area" => descending
                ? detections.OrderByDescending(detection => detection.Detection.Area).ThenBy(detection => detection.EvalX)
                : detections.OrderBy(detection => detection.Detection.Area).ThenBy(detection => detection.EvalX),
            _ => descending
                ? detections.OrderByDescending(detection => detection.EvalX).ThenBy(detection => detection.EvalY)
                : detections.OrderBy(detection => detection.EvalX).ThenBy(detection => detection.EvalY)
        };

        return ordered.ToList();
    }

    private static double GetTopY(SequenceDetection detection)
    {
        return detection.EvalY - (detection.Detection.Height / 2.0);
    }

    private static List<List<SequenceDetection>> ClusterRows(IReadOnlyList<SequenceDetection> detections, double tolerance)
    {
        if (detections.Count == 0)
        {
            return new List<List<SequenceDetection>>();
        }

        var rows = new List<List<SequenceDetection>>();
        foreach (var detection in detections.OrderBy(x => x.EvalY).ThenBy(x => x.EvalX))
        {
            var row = rows
                .Select(group => new { Group = group, AverageY = group.Average(item => item.EvalY) })
                .OrderBy(item => Math.Abs(item.AverageY - detection.EvalY))
                .FirstOrDefault(item => Math.Abs(item.AverageY - detection.EvalY) <= tolerance);

            if (row == null)
            {
                rows.Add(new List<SequenceDetection> { detection });
            }
            else
            {
                row.Group.Add(detection);
            }
        }

        return rows;
    }

    private static List<SequenceDetection> FlattenRows(IReadOnlyList<List<SequenceDetection>> rows, string sortBy, string direction)
    {
        var sortedRows = direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderByDescending(row => row.Average(item => item.EvalY))
            : rows.OrderBy(row => row.Average(item => item.EvalY));

        var flattened = new List<SequenceDetection>();
        foreach (var row in sortedRows)
        {
            flattened.AddRange(SortSequenceDetections(row, sortBy, direction));
        }

        return flattened;
    }

    private static List<Point2f> BuildSlotOrder(IReadOnlyList<Point2f> slots, string direction, double rowTolerance)
    {
        if (slots.Count == 0)
        {
            return new List<Point2f>();
        }

        var rows = new List<List<Point2f>>();
        foreach (var slot in slots.OrderBy(point => point.Y).ThenBy(point => point.X))
        {
            var row = rows
                .Select(group => new { Group = group, AverageY = group.Average(point => point.Y) })
                .OrderBy(item => Math.Abs(item.AverageY - slot.Y))
                .FirstOrDefault(item => Math.Abs(item.AverageY - slot.Y) <= rowTolerance);

            if (row == null)
            {
                rows.Add(new List<Point2f> { slot });
            }
            else
            {
                row.Group.Add(slot);
            }
        }

        var sortedRows = direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderByDescending(row => row.Average(point => point.Y))
            : rows.OrderBy(row => row.Average(point => point.Y));
        var descendingX = direction.Equals("RightToLeft", StringComparison.OrdinalIgnoreCase) || direction.Equals("Descending", StringComparison.OrdinalIgnoreCase);

        var ordered = new List<Point2f>();
        foreach (var row in sortedRows)
        {
            ordered.AddRange(descendingX ? row.OrderByDescending(point => point.X) : row.OrderBy(point => point.X));
        }

        return ordered;
    }

    private static int CountPointRows(IReadOnlyList<Point2f> points, double rowTolerance)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        var rows = new List<double>();
        foreach (var point in points.OrderBy(point => point.Y))
        {
            var matched = false;
            for (var i = 0; i < rows.Count; i++)
            {
                if (Math.Abs(rows[i] - point.Y) <= rowTolerance)
                {
                    rows[i] = (rows[i] + point.Y) / 2.0;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                rows.Add(point.Y);
            }
        }

        return rows.Count;
    }

    private static void AssignToSlots(
        IReadOnlyList<SequenceDetection> detections,
        IReadOnlyList<Point2f> slotOrder,
        IReadOnlyList<string> expectedLabels,
        double slotTolerance,
        out List<AssignmentRecord> assignment,
        out List<SequenceDetection> orderedDetections,
        out List<SequenceDetection> unassignedDetections)
    {
        assignment = new List<AssignmentRecord>(slotOrder.Count);
        orderedDetections = new List<SequenceDetection>();
        if (slotOrder.Count == 0)
        {
            unassignedDetections = detections.ToList();
            return;
        }

        if (detections.Count == 0)
        {
            for (var slotIndex = 0; slotIndex < slotOrder.Count; slotIndex++)
            {
                var slot = slotOrder[slotIndex];
                assignment.Add(new AssignmentRecord(slotIndex, expectedLabels.ElementAtOrDefault(slotIndex) ?? string.Empty, null, slot.X, slot.Y, -1));
            }

            unassignedDetections = new List<SequenceDetection>();
            return;
        }

        const double unassignedPenalty = 1_000_000_000.0;
        const double impossiblePenalty = 1_000_000_000_000.0;
        const double confidenceTieBreakerScale = 1e-6;

        var slotCount = slotOrder.Count;
        var detectionCount = detections.Count;
        var columnCount = detectionCount + slotCount;
        var costMatrix = new double[slotCount][];

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var slot = slotOrder[slotIndex];
            var row = new double[columnCount];

            for (var detectionIndex = 0; detectionIndex < detectionCount; detectionIndex++)
            {
                var detection = detections[detectionIndex];
                var distance = ComputeDistance(detection.EvalX, detection.EvalY, slot.X, slot.Y);
                if (distance > slotTolerance)
                {
                    row[detectionIndex] = impossiblePenalty;
                    continue;
                }

                var confidence = Math.Clamp((double)detection.Detection.Confidence, 0.0, 1.0);
                row[detectionIndex] = distance + ((1.0 - confidence) * confidenceTieBreakerScale);
            }

            for (var dummyIndex = 0; dummyIndex < slotCount; dummyIndex++)
            {
                var columnIndex = detectionCount + dummyIndex;
                row[columnIndex] = dummyIndex == slotIndex ? unassignedPenalty : impossiblePenalty;
            }

            costMatrix[slotIndex] = row;
        }

        var matchedColumns = SolveHungarian(costMatrix);
        var assignedDetectionIndexes = new HashSet<int>();

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var slot = slotOrder[slotIndex];
            var column = matchedColumns[slotIndex];
            var expectedLabel = expectedLabels.ElementAtOrDefault(slotIndex) ?? string.Empty;

            if (column < 0 || column >= detectionCount)
            {
                assignment.Add(new AssignmentRecord(slotIndex, expectedLabel, null, slot.X, slot.Y, -1));
                continue;
            }

            var matchedDetection = detections[column];
            var distance = ComputeDistance(matchedDetection.EvalX, matchedDetection.EvalY, slot.X, slot.Y);
            if (distance > slotTolerance)
            {
                assignment.Add(new AssignmentRecord(slotIndex, expectedLabel, null, slot.X, slot.Y, -1));
                continue;
            }

            assignedDetectionIndexes.Add(column);
            orderedDetections.Add(matchedDetection);
            assignment.Add(new AssignmentRecord(slotIndex, expectedLabel, matchedDetection, slot.X, slot.Y, distance));
        }

        unassignedDetections = detections
            .Where((_, index) => !assignedDetectionIndexes.Contains(index))
            .ToList();
    }

    private static string ResolveGroupingMode(string groupingMode, int slotCount, int detectionCount, double rowTolerance)
    {
        return NormalizeGroupingMode(groupingMode) switch
        {
            "Auto" when slotCount > 0 => "SlotAssignment",
            "Auto" when detectionCount > 1 && rowTolerance > 0 => "RowCluster",
            "Auto" => "SingleRow",
            var resolved => resolved
        };
    }

    private static string NormalizeGroupingMode(string groupingMode)
    {
        return groupingMode.Trim() switch
        {
            var value when value.Equals("RowCluster", StringComparison.OrdinalIgnoreCase) => "RowCluster",
            var value when value.Equals("SlotAssignment", StringComparison.OrdinalIgnoreCase) => "SlotAssignment",
            var value when value.Equals("Auto", StringComparison.OrdinalIgnoreCase) => "Auto",
            _ => "SingleRow"
        };
    }

    private static string NormalizeSortBy(string sortBy, string direction)
    {
        if (direction.Equals("TopToBottom", StringComparison.OrdinalIgnoreCase) ||
            direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase))
        {
            return sortBy.Equals("TopY", StringComparison.OrdinalIgnoreCase)
                ? "TopY"
                : "CenterY";
        }

        return sortBy;
    }

    private static bool IsDescending(string direction)
    {
        return direction.Equals("Descending", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("RightToLeft", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase);
    }

    private static double ResolveRowTolerance(IReadOnlyList<SequenceDetection> detections, double configuredTolerance)
    {
        if (configuredTolerance > 0)
        {
            return configuredTolerance;
        }

        if (detections.Count == 0)
        {
            return 8.0;
        }

        var medianHeight = detections.Select(d => d.Detection.Height).OrderBy(x => x).ElementAt(detections.Count / 2);
        return Math.Max(6.0, medianHeight * 0.75);
    }

    private static double ResolveSlotTolerance(IReadOnlyList<SequenceDetection> detections, double configuredTolerance)
    {
        if (configuredTolerance > 0)
        {
            return configuredTolerance;
        }

        if (detections.Count == 0)
        {
            return 20.0;
        }

        var medianSize = detections.Select(d => Math.Max(d.Detection.Width, d.Detection.Height)).OrderBy(x => x).ElementAt(detections.Count / 2);
        return Math.Max(12.0, medianSize * 1.5);
    }

    private static List<string> ParseLabels(string rawLabels)
    {
        return rawLabels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();
    }

    private static List<string> ComputeMissingLabels(IReadOnlyList<string> expectedLabels, IReadOnlyList<string> actualOrder)
    {
        var counts = actualOrder
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var label in expectedLabels)
        {
            if (!counts.TryGetValue(label, out var count) || count == 0)
            {
                missing.Add(label);
                continue;
            }

            counts[label] = count - 1;
        }

        return missing;
    }

    private static DetectionResult CloneDetection(DetectionResult detection)
    {
        return new DetectionResult(detection.Label, detection.Confidence, detection.X, detection.Y, detection.Width, detection.Height);
    }

    private static string FormatLabels(IReadOnlyCollection<string> labels)
    {
        return labels.Count == 0 ? "<empty>" : string.Join(" -> ", labels);
    }

    private static Dictionary<string, object> ToAssignmentDictionary(AssignmentRecord assignment)
    {
        return new Dictionary<string, object>
        {
            ["SlotIndex"] = assignment.SlotIndex,
            ["ExpectedLabel"] = assignment.ExpectedLabel,
            ["ActualLabel"] = assignment.Detection?.Detection.Label ?? string.Empty,
            ["Assigned"] = assignment.Detection != null,
            ["SlotX"] = assignment.SlotX,
            ["SlotY"] = assignment.SlotY,
            ["Distance"] = assignment.Distance,
            ["DetectionCenterX"] = assignment.Detection?.EvalX ?? 0.0,
            ["DetectionCenterY"] = assignment.Detection?.EvalY ?? 0.0
        };
    }

    private static double ComputeDistance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int[] SolveHungarian(IReadOnlyList<double[]> costMatrix)
    {
        var rowCount = costMatrix.Count;
        if (rowCount == 0)
        {
            return Array.Empty<int>();
        }

        var columnCount = costMatrix[0].Length;
        if (columnCount == 0 || rowCount > columnCount)
        {
            throw new InvalidOperationException("Hungarian matching requires a non-empty cost matrix with columns >= rows.");
        }

        var u = new double[rowCount + 1];
        var v = new double[columnCount + 1];
        var p = new int[columnCount + 1];
        var way = new int[columnCount + 1];

        for (var row = 1; row <= rowCount; row++)
        {
            p[0] = row;
            var minv = Enumerable.Repeat(double.PositiveInfinity, columnCount + 1).ToArray();
            var used = new bool[columnCount + 1];
            var column0 = 0;

            do
            {
                used[column0] = true;
                var row0 = p[column0];
                var delta = double.PositiveInfinity;
                var column1 = 0;

                for (var column = 1; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        continue;
                    }

                    var current = costMatrix[row0 - 1][column - 1] - u[row0] - v[column];
                    if (current < minv[column])
                    {
                        minv[column] = current;
                        way[column] = column0;
                    }

                    if (minv[column] < delta)
                    {
                        delta = minv[column];
                        column1 = column;
                    }
                }

                for (var column = 0; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        u[p[column]] += delta;
                        v[column] -= delta;
                    }
                    else
                    {
                        minv[column] -= delta;
                    }
                }

                column0 = column1;
            }
            while (p[column0] != 0);

            do
            {
                var column1 = way[column0];
                p[column0] = p[column1];
                column0 = column1;
            }
            while (column0 != 0);
        }

        var assignment = Enumerable.Repeat(-1, rowCount).ToArray();
        for (var column = 1; column <= columnCount; column++)
        {
            if (p[column] > 0)
            {
                assignment[p[column] - 1] = column - 1;
            }
        }

        return assignment;
    }

    private bool TryResolveSlotPoints(
        Dictionary<string, object>? inputs,
        Operator @operator,
        out List<Point2f> points,
        out string? error)
    {
        points = new List<Point2f>();
        error = null;

        if (inputs != null &&
            inputs.TryGetValue("SlotPoints", out var rawInput))
        {
            if (!TryParsePointCollection(rawInput, out var inputPoints))
            {
                if (IsExplicitlyEmptyPointCollection(rawInput))
                {
                    return true;
                }

                error = "SlotPoints input contains invalid point data; each point must provide numeric x and y.";
                return false;
            }

            points = inputPoints;
            return true;
        }

        var expectedSlots = GetStringParam(@operator, "ExpectedSlots", string.Empty);
        if (string.IsNullOrWhiteSpace(expectedSlots))
        {
            return true;
        }

        if (!TryParsePointCollection(expectedSlots, out var parameterPoints))
        {
            error = "ExpectedSlots must be a valid point list; each point must provide numeric x and y.";
            return false;
        }

        points = parameterPoints;
        return true;
    }

    private static bool IsExplicitlyEmptyPointCollection(object? raw)
    {
        if (raw == null)
        {
            return true;
        }

        if (raw is string text)
        {
            return text.Trim().Equals("[]", StringComparison.Ordinal);
        }

        if (raw is JsonElement element &&
            element.ValueKind == JsonValueKind.Array &&
            element.GetArrayLength() == 0)
        {
            return true;
        }

        if (raw is IEnumerable enumerable &&
            raw is not string &&
            raw is not IDictionary)
        {
            foreach (var _ in enumerable)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private PerspectiveContext ResolvePerspectiveContext(Dictionary<string, object>? inputs, Operator @operator, out string? error)
    {
        error = null;

        if (!TryResolvePointSet(inputs, @operator, "PerspectiveSrcPoints", "PerspectiveSrcPointsJson", out var srcPoints, out var srcProvided, out var srcError))
        {
            error = srcError;
            return PerspectiveContext.Empty;
        }

        if (!TryResolvePointSet(inputs, @operator, "PerspectiveDstPoints", "PerspectiveDstPointsJson", out var dstPoints, out var dstProvided, out var dstError))
        {
            error = dstError;
            return PerspectiveContext.Empty;
        }

        if (srcProvided ^ dstProvided)
        {
            error = "Perspective source/destination points must be provided in pairs and contain at least 4 points.";
            return PerspectiveContext.Empty;
        }

        if (!srcProvided)
        {
            return PerspectiveContext.Empty;
        }

        using var transform = Cv2.GetPerspectiveTransform(srcPoints.Take(4).ToArray(), dstPoints.Take(4).ToArray());
        return new PerspectiveContext(CloneMatrix(transform), "PerspectiveTransform");
    }

    private static void ApplyPerspective(IEnumerable<SequenceDetection> detections, PerspectiveContext perspective)
    {
        foreach (var detection in detections)
        {
            var corrected = TransformPoint(new Point2d(detection.EvalX, detection.EvalY), perspective.Matrix);
            detection.EvalX = corrected.X;
            detection.EvalY = corrected.Y;
        }
    }

    private static void ApplyPerspective(IList<Point2f> points, PerspectiveContext perspective)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var corrected = TransformPoint(new Point2d(points[i].X, points[i].Y), perspective.Matrix);
            points[i] = new Point2f((float)corrected.X, (float)corrected.Y);
        }
    }

    private static Point2d TransformPoint(Point2d point, Mat transform)
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

        var denominator = (m20 * point.X) + (m21 * point.Y) + m22;
        if (Math.Abs(denominator) < 1e-9)
        {
            return point;
        }

        var x = ((m00 * point.X) + (m01 * point.Y) + m02) / denominator;
        var y = ((m10 * point.X) + (m11 * point.Y) + m12) / denominator;
        return new Point2d(x, y);
    }

    private static Mat CloneMatrix(Mat transform)
    {
        var cloned = new Mat();
        transform.CopyTo(cloned);
        return cloned;
    }

    private bool TryResolvePointSet(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string inputPort,
        string jsonParamName,
        out List<Point2f> points,
        out bool provided,
        out string? error)
    {
        points = new List<Point2f>();
        provided = false;
        error = null;

        if (inputs != null && inputs.TryGetValue(inputPort, out var rawInput))
        {
            provided = true;

            if (!TryParsePointCollection(rawInput, out var parsedInput))
            {
                error = $"{inputPort} contains invalid point data; each point must provide numeric x and y.";
                return false;
            }

            if (parsedInput.Count < 4)
            {
                error = $"{inputPort} must contain at least 4 points.";
                return false;
            }

            points = parsedInput;
            return true;
        }

        var json = GetStringParam(@operator, jsonParamName, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        provided = true;
        if (!TryParsePointCollection(json, out var parsedJson))
        {
            error = $"{jsonParamName} contains invalid point data; each point must provide numeric x and y.";
            return false;
        }

        if (parsedJson.Count < 4)
        {
            error = $"{jsonParamName} must contain at least 4 points.";
            return false;
        }

        points = parsedJson;
        return true;
    }

    private static bool TryParsePointCollection(object? raw, out List<Point2f> points)
    {
        points = new List<Point2f>();
        switch (raw)
        {
            case null:
                return false;
            case string text when TryParsePointString(text, out points):
                return points.Count > 0;
            case IEnumerable<Position> positions:
                points = positions.Select(position => new Point2f((float)position.X, (float)position.Y)).ToList();
                return points.Count > 0;
            case IEnumerable<Point2f> point2Fs:
                points = point2Fs.ToList();
                return points.Count > 0;
            case IEnumerable<Point> cvPoints:
                points = cvPoints.Select(point => new Point2f(point.X, point.Y)).ToList();
                return points.Count > 0;
            case IEnumerable<object> list:
                foreach (var item in list)
                {
                    if (!TryParsePoint(item, out var point))
                    {
                        return false;
                    }

                    points.Add(point);
                }

                return points.Count > 0;
            default:
                return false;
        }
    }

    private static bool TryParsePointString(string raw, out List<Point2f> points)
    {
        points = new List<Point2f>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (!TryParsePoint(element, out var point))
                    {
                        return false;
                    }

                    points.Add(point);
                }

                return points.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        foreach (var pair in trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            points.Add(new Point2f(x, y));
        }

        return points.Count > 0;
    }

    private static bool TryParsePoint(object? raw, out Point2f point)
    {
        point = default;
        switch (raw)
        {
            case null:
                return false;
            case Point2f point2F:
                point = point2F;
                return true;
            case Point cvPoint:
                point = new Point2f(cvPoint.X, cvPoint.Y);
                return true;
            case Position position:
                point = new Point2f((float)position.X, (float)position.Y);
                return true;
            case IDictionary<string, object> dict:
                if (TryGetFloat(dict, "X", out var x) && TryGetFloat(dict, "Y", out var y))
                {
                    point = new Point2f(x, y);
                    return true;
                }

                return false;
            case JsonElement element:
                return TryParsePoint(element, out point);
            default:
                return false;
        }
    }

    private static bool TryParsePoint(JsonElement element, out Point2f point)
    {
        point = default;
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() >= 2)
        {
            if (!TryParseElementAsFloat(element[0], out var x) || !TryParseElementAsFloat(element[1], out var y))
            {
                return false;
            }

            point = new Point2f(x, y);
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (!TryGetPropertyAsFloat(element, out var x, "x", "X") ||
                !TryGetPropertyAsFloat(element, out var y, "y", "Y"))
            {
                return false;
            }

            point = new Point2f(x, y);
            return true;
        }

        return false;
    }

    private static bool TryParseElementAsFloat(JsonElement element, out float value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                value = element.GetSingle();
                return true;
            case JsonValueKind.String:
                return float.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            default:
                value = 0f;
                return false;
        }
    }

    private static bool TryGetPropertyAsFloat(JsonElement element, out float value, params string[] names)
    {
        value = 0f;
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var propertyValue) &&
                TryParseElementAsFloat(propertyValue, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFloat(IDictionary<string, object> dict, string key, out float value)
    {
        value = 0f;
        var entry = dict.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
        {
            return false;
        }

        return entry.Value switch
        {
            float floatValue => (value = floatValue) == floatValue,
            double doubleValue => (value = (float)doubleValue) == (float)doubleValue,
            int intValue => (value = intValue) == intValue,
            long longValue => (value = longValue) == longValue,
            _ => float.TryParse(entry.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
        };
    }

    private sealed class SequenceDetection
    {
        public required DetectionResult Detection { get; init; }
        public required int OriginalIndex { get; init; }
        public double EvalX { get; set; }
        public double EvalY { get; set; }

        public static SequenceDetection Create(DetectionResult detection, int index)
        {
            return new SequenceDetection
            {
                Detection = detection,
                OriginalIndex = index,
                EvalX = detection.CenterX,
                EvalY = detection.CenterY
            };
        }
    }

    private sealed record AssignmentRecord(int SlotIndex, string ExpectedLabel, SequenceDetection? Detection, double SlotX, double SlotY, double Distance);

    private sealed record PerspectiveContext(Mat Matrix, string Source)
    {
        public static PerspectiveContext Empty => new(new Mat(), "None");
        public bool IsValid => Matrix != null && !Matrix.Empty();
    }
}
