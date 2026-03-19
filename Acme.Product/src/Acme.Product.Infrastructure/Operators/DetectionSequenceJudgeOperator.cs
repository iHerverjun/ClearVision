using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;

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
    })]
[InputPort("Detections", "Detections", PortDataType.DetectionList, IsRequired = true)]
[OutputPort("IsMatch", "Is Match", PortDataType.Boolean)]
[OutputPort("ActualOrder", "Actual Order", PortDataType.Any)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("MissingLabels", "Missing Labels", PortDataType.Any)]
[OutputPort("DuplicateLabels", "Duplicate Labels", PortDataType.Any)]
[OutputPort("SortedDetections", "Sorted Detections", PortDataType.DetectionList)]
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
public sealed class DetectionSequenceJudgeOperator : OperatorBase
{
    private static readonly string[] ValidSortBy =
    {
        "CenterX",
        "CenterY",
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
        var configuredExpectedCount = GetIntParam(@operator, "ExpectedCount", 0, 0, 256);
        var minConfidence = GetDoubleParam(@operator, "MinConfidence", 0.0, 0.0, 1.0);
        var allowMissing = GetBoolParam(@operator, "AllowMissing", false);
        var allowDuplicate = GetBoolParam(@operator, "AllowDuplicate", false);

        var rawDetections = DetectionOutputInspector.ExtractDetections(detectionValue);
        var filteredDetections = rawDetections
            .Where(detection => detection.Confidence >= minConfidence)
            .Select(CloneDetection)
            .ToList();

        var sortedDetections = SortDetections(filteredDetections, sortBy, direction);
        var actualOrder = sortedDetections
            .Select(detection => detection.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var expectedCount = configuredExpectedCount > 0
            ? configuredExpectedCount
            : expectedLabels.Count;

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

        if (!allowMissing && missingLabels.Count > 0)
        {
            reasons.Add($"Missing labels: {string.Join(", ", missingLabels)}.");
        }

        if (!allowDuplicate && duplicateLabels.Count > 0)
        {
            reasons.Add($"Duplicate labels: {string.Join(", ", duplicateLabels)}.");
        }

        if (expectedCount > 0 && sortedDetections.Count != expectedCount)
        {
            reasons.Add($"Expected {expectedCount} detections but got {sortedDetections.Count}.");
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

        var result = new Dictionary<string, object>
        {
            ["IsMatch"] = isMatch,
            ["ActualOrder"] = actualOrder,
            ["Count"] = sortedDetections.Count,
            ["DetectionCount"] = sortedDetections.Count,
            ["MissingLabels"] = missingLabels,
            ["DuplicateLabels"] = duplicateLabels,
            ["SortedDetections"] = new DetectionList(sortedDetections),
            ["ExpectedLabels"] = expectedLabels,
            ["ExpectedCount"] = expectedCount,
            ["RequiredMinConfidence"] = minConfidence,
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

        return ValidationResult.Valid();
    }

    private static List<DetectionResult> SortDetections(
        IEnumerable<DetectionResult> detections,
        string sortBy,
        string direction)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy, direction);
        var descending = IsDescending(direction);

        var ordered = normalizedSortBy switch
        {
            "CenterY" => descending
                ? detections.OrderByDescending(detection => detection.CenterY)
                    .ThenBy(detection => detection.CenterX)
                : detections.OrderBy(detection => detection.CenterY)
                    .ThenBy(detection => detection.CenterX),
            "Confidence" => descending
                ? detections.OrderByDescending(detection => detection.Confidence)
                    .ThenBy(detection => detection.CenterX)
                : detections.OrderBy(detection => detection.Confidence)
                    .ThenBy(detection => detection.CenterX),
            "Area" => descending
                ? detections.OrderByDescending(detection => detection.Area)
                    .ThenBy(detection => detection.CenterX)
                : detections.OrderBy(detection => detection.Area)
                    .ThenBy(detection => detection.CenterX),
            _ => descending
                ? detections.OrderByDescending(detection => detection.CenterX)
                    .ThenBy(detection => detection.CenterY)
                : detections.OrderBy(detection => detection.CenterX)
                    .ThenBy(detection => detection.CenterY)
        };

        return ordered
            .Select(CloneDetection)
            .ToList();
    }

    private static string NormalizeSortBy(string sortBy, string direction)
    {
        if (direction.Equals("TopToBottom", StringComparison.OrdinalIgnoreCase) ||
            direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase))
        {
            return "CenterY";
        }

        return sortBy;
    }

    private static bool IsDescending(string direction)
    {
        return direction.Equals("Descending", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("RightToLeft", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("BottomToTop", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ParseLabels(string rawLabels)
    {
        return rawLabels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();
    }

    private static List<string> ComputeMissingLabels(
        IReadOnlyList<string> expectedLabels,
        IReadOnlyList<string> actualOrder)
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
        return new DetectionResult(
            detection.Label,
            detection.Confidence,
            detection.X,
            detection.Y,
            detection.Width,
            detection.Height);
    }

    private static string FormatLabels(IReadOnlyCollection<string> labels)
    {
        return labels.Count == 0 ? "<empty>" : string.Join(" -> ", labels);
    }
}
