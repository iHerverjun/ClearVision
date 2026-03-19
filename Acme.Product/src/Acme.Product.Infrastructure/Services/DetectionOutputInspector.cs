using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Services;

public sealed class DetectionOutputSummary
{
    public List<DetectionResult> Detections { get; init; } = new();
    public Dictionary<string, int> PerClassCount { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ActualOrder { get; init; } = new();
    public List<string> ExpectedLabels { get; init; } = new();
    public List<string> MissingLabels { get; init; } = new();
    public List<string> DuplicateLabels { get; init; } = new();
    public int DetectionCount { get; init; }
    public int? DeclaredCount { get; init; }
    public int? ExpectedCount { get; init; }
    public double? MinConfidence { get; init; }
    public double RequiredMinConfidence { get; init; } = 0.5;

    public bool HasDetectionSemantics =>
        DetectionCount > 0 ||
        DeclaredCount.HasValue ||
        ExpectedCount.HasValue ||
        ExpectedLabels.Count > 0 ||
        MissingLabels.Count > 0 ||
        DuplicateLabels.Count > 0;
}

public static class DetectionOutputInspector
{
    private static readonly string[] DetectionKeys =
    {
        "SortedDetections",
        "DetectionList",
        "Detections",
        "Objects",
        "Defects",
        "Blobs"
    };

    public static DetectionOutputSummary Inspect(Dictionary<string, object>? outputData)
    {
        if (outputData == null || outputData.Count == 0)
        {
            return new DetectionOutputSummary();
        }

        var detections = TryExtractDetections(outputData, out var detectionSourceKey);
        var expectedLabels = ExtractStringList(outputData, "ExpectedLabels", "ExpectedOrder");
        var actualOrder = ExtractStringList(outputData, "ActualOrder", "SortedLabels");
        if (actualOrder.Count == 0)
        {
            actualOrder = BuildActualOrder(detections, detectionSourceKey);
        }

        var missingLabels = ExtractStringList(outputData, "MissingLabels");
        if (missingLabels.Count == 0 && expectedLabels.Count > 0)
        {
            missingLabels = ComputeMissingLabels(expectedLabels, actualOrder);
        }

        var duplicateLabels = ExtractStringList(outputData, "DuplicateLabels");
        if (duplicateLabels.Count == 0)
        {
            duplicateLabels = actualOrder
                .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
        }

        var perClassCount = detections
            .GroupBy(detection => detection.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var requiredMinConfidence = TryReadDouble(outputData, out var configuredMinConfidence,
            "ConfiguredMinConfidence",
            "RequiredMinConfidence",
            "MinRequiredConfidence")
            ? configuredMinConfidence
            : 0.5;

        var expectedCount = TryReadInt(outputData, out var explicitExpectedCount, "ExpectedCount", "TargetCount")
            ? explicitExpectedCount
            : expectedLabels.Count > 0
                ? expectedLabels.Count
                : null;

        var declaredCount = TryReadInt(outputData, out var explicitCount,
            "DetectionCount",
            "ObjectCount",
            "DefectCount")
            ? explicitCount
            : null;

        return new DetectionOutputSummary
        {
            Detections = detections,
            DetectionCount = detections.Count,
            DeclaredCount = declaredCount,
            ExpectedCount = expectedCount,
            ActualOrder = actualOrder,
            ExpectedLabels = expectedLabels,
            MissingLabels = missingLabels,
            DuplicateLabels = duplicateLabels,
            PerClassCount = perClassCount,
            MinConfidence = detections.Count > 0 ? detections.Min(detection => (double)detection.Confidence) : null,
            RequiredMinConfidence = requiredMinConfidence
        };
    }

    private static List<DetectionResult> TryExtractDetections(
        Dictionary<string, object> outputData,
        out string? detectionSourceKey)
    {
        detectionSourceKey = null;
        foreach (var key in DetectionKeys)
        {
            if (!outputData.TryGetValue(key, out var value))
            {
                continue;
            }

            var detections = ExtractDetections(value);
            if (detections.Count == 0)
            {
                continue;
            }

            detectionSourceKey = key;
            return detections;
        }

        return new List<DetectionResult>();
    }

    public static List<DetectionResult> ExtractDetections(object? value)
    {
        return value switch
        {
            null => new List<DetectionResult>(),
            DetectionList detectionList => detectionList.Detections.Select(Clone).ToList(),
            IEnumerable<DetectionResult> typedDetections => typedDetections.Select(Clone).ToList(),
            JsonElement element when element.ValueKind == JsonValueKind.Array => ExtractDetectionsFromJsonArray(element),
            string => new List<DetectionResult>(),
            IDictionary dictionary => ExtractDetectionsFromDictionaryEnumerable(new[] { dictionary }),
            IEnumerable enumerable => ExtractDetectionsFromEnumerable(enumerable),
            _ => new List<DetectionResult>()
        };
    }

    private static List<DetectionResult> ExtractDetectionsFromEnumerable(IEnumerable enumerable)
    {
        var detections = new List<DetectionResult>();
        foreach (var item in enumerable)
        {
            if (TryConvertToDetection(item, out var detection))
            {
                detections.Add(detection);
            }
        }

        return detections;
    }

    private static List<DetectionResult> ExtractDetectionsFromDictionaryEnumerable(IEnumerable<IDictionary> dictionaries)
    {
        var detections = new List<DetectionResult>();
        foreach (var dictionary in dictionaries)
        {
            if (TryConvertDictionaryToDetection(dictionary, out var detection))
            {
                detections.Add(detection);
            }
        }

        return detections;
    }

    private static List<DetectionResult> ExtractDetectionsFromJsonArray(JsonElement element)
    {
        var detections = new List<DetectionResult>();
        foreach (var item in element.EnumerateArray())
        {
            if (TryConvertJsonElementToDetection(item, out var detection))
            {
                detections.Add(detection);
            }
        }

        return detections;
    }

    private static bool TryConvertToDetection(object? item, out DetectionResult detection)
    {
        switch (item)
        {
            case null:
                detection = new DetectionResult();
                return false;
            case DetectionResult typedDetection:
                detection = Clone(typedDetection);
                return true;
            case IDictionary dictionary:
                return TryConvertDictionaryToDetection(dictionary, out detection);
            case JsonElement jsonElement:
                return TryConvertJsonElementToDetection(jsonElement, out detection);
            default:
                return TryConvertObjectToDetection(item, out detection);
        }
    }

    private static bool TryConvertDictionaryToDetection(IDictionary dictionary, out DetectionResult detection)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string key)
            {
                values[key] = entry.Value;
            }
        }

        return TryBuildDetection(
            values.TryGetValue("Label", out var label) ? label?.ToString() : values.TryGetValue("ClassName", out var className) ? className?.ToString() : null,
            values.TryGetValue("Confidence", out var confidence) ? confidence : values.TryGetValue("Score", out var score) ? score : null,
            values.TryGetValue("X", out var x) ? x : values.TryGetValue("Left", out var left) ? left : null,
            values.TryGetValue("Y", out var y) ? y : values.TryGetValue("Top", out var top) ? top : null,
            values.TryGetValue("Width", out var width) ? width : null,
            values.TryGetValue("Height", out var height) ? height : null,
            out detection);
    }

    private static bool TryConvertJsonElementToDetection(JsonElement element, out DetectionResult detection)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            detection = new DetectionResult();
            return false;
        }

        string? label = null;
        object? confidence = null;
        object? x = null;
        object? y = null;
        object? width = null;
        object? height = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "Label":
                case "label":
                case "ClassName":
                case "className":
                    label = property.Value.GetString();
                    break;
                case "Confidence":
                case "confidence":
                case "Score":
                case "score":
                    confidence = property.Value;
                    break;
                case "X":
                case "x":
                case "Left":
                case "left":
                    x = property.Value;
                    break;
                case "Y":
                case "y":
                case "Top":
                case "top":
                    y = property.Value;
                    break;
                case "Width":
                case "width":
                    width = property.Value;
                    break;
                case "Height":
                case "height":
                    height = property.Value;
                    break;
            }
        }

        return TryBuildDetection(label, confidence, x, y, width, height, out detection);
    }

    private static bool TryConvertObjectToDetection(object item, out DetectionResult detection)
    {
        var type = item.GetType();
        object? ReadProperty(string name) => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(item);

        return TryBuildDetection(
            ReadProperty("Label")?.ToString() ?? ReadProperty("ClassName")?.ToString(),
            ReadProperty("Confidence") ?? ReadProperty("Score"),
            ReadProperty("X") ?? ReadProperty("Left"),
            ReadProperty("Y") ?? ReadProperty("Top"),
            ReadProperty("Width"),
            ReadProperty("Height"),
            out detection);
    }

    private static bool TryBuildDetection(
        string? label,
        object? confidence,
        object? x,
        object? y,
        object? width,
        object? height,
        out DetectionResult detection)
    {
        if (!TryReadFloat(x, out var xValue) ||
            !TryReadFloat(y, out var yValue) ||
            !TryReadFloat(width, out var widthValue) ||
            !TryReadFloat(height, out var heightValue))
        {
            detection = new DetectionResult();
            return false;
        }

        var confidenceValue = TryReadFloat(confidence, out var parsedConfidence) ? parsedConfidence : 0f;
        detection = new DetectionResult(label ?? string.Empty, confidenceValue, xValue, yValue, widthValue, heightValue);
        return true;
    }

    private static List<string> BuildActualOrder(List<DetectionResult> detections, string? detectionSourceKey)
    {
        if (detections.Count == 0)
        {
            return new List<string>();
        }

        var ordered = string.Equals(detectionSourceKey, "SortedDetections", StringComparison.OrdinalIgnoreCase)
            ? detections
            : detections
                .OrderBy(detection => detection.CenterX)
                .ThenBy(detection => detection.CenterY)
                .ToList();

        return ordered
            .Select(detection => detection.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();
    }

    private static List<string> ComputeMissingLabels(IReadOnlyList<string> expectedLabels, IReadOnlyList<string> actualOrder)
    {
        var counts = actualOrder
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var label in expectedLabels.Where(label => !string.IsNullOrWhiteSpace(label)))
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

    private static List<string> ExtractStringList(Dictionary<string, object> outputData, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!outputData.TryGetValue(key, out var value))
            {
                continue;
            }

            var list = value switch
            {
                null => new List<string>(),
                string raw => raw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                IEnumerable<string> strings => strings
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList(),
                JsonElement element when element.ValueKind == JsonValueKind.Array => element
                    .EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim())
                    .ToList(),
                IEnumerable enumerable => enumerable
                    .Cast<object?>()
                    .Select(item => item?.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim())
                    .ToList(),
                _ => new List<string>()
            };

            if (list.Count > 0)
            {
                return list;
            }
        }

        return new List<string>();
    }

    private static bool TryReadInt(Dictionary<string, object> outputData, out int? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (outputData.TryGetValue(key, out var candidate) && TryReadInt(candidate, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryReadInt(object? value, out int parsed)
    {
        switch (value)
        {
            case null:
                parsed = 0;
                return false;
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue:
                parsed = (int)longValue;
                return true;
            case double doubleValue:
                parsed = (int)Math.Round(doubleValue);
                return true;
            case float floatValue:
                parsed = (int)Math.Round(floatValue);
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var jsonInt):
                parsed = jsonInt;
                return true;
            default:
                return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }
    }

    private static bool TryReadDouble(Dictionary<string, object> outputData, out double parsed, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (outputData.TryGetValue(key, out var value) && TryReadDouble(value, out parsed))
            {
                return true;
            }
        }

        parsed = 0;
        return false;
    }

    private static bool TryReadDouble(object? value, out double parsed)
    {
        switch (value)
        {
            case null:
                parsed = 0;
                return false;
            case double doubleValue:
                parsed = doubleValue;
                return true;
            case float floatValue:
                parsed = floatValue;
                return true;
            case decimal decimalValue:
                parsed = (double)decimalValue;
                return true;
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue:
                parsed = longValue;
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out var jsonDouble):
                parsed = jsonDouble;
                return true;
            default:
                return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }
    }

    private static bool TryReadFloat(object? value, out float parsed)
    {
        if (TryReadDouble(value, out var doubleValue))
        {
            parsed = (float)doubleValue;
            return true;
        }

        parsed = 0f;
        return false;
    }

    private static DetectionResult Clone(DetectionResult detection)
    {
        return new DetectionResult(
            detection.Label,
            detection.Confidence,
            detection.X,
            detection.Y,
            detection.Width,
            detection.Height);
    }
}
