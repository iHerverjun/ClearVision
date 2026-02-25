using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using DetectionListValue = Acme.Product.Core.ValueObjects.DetectionList;
using DetectionResultValue = Acme.Product.Core.ValueObjects.DetectionResult;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Filters detection boxes by area/class/region/score.
/// </summary>
public class BoxFilterOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BoxFilter;

    public BoxFilterOperator(ILogger<BoxFilterOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Detections", out var detObj) || !TryParseDetectionList(detObj, out var detections))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Detections' is required"));
        }

        var mode = GetStringParam(@operator, "FilterMode", "Area");
        var minArea = GetDoubleParam(@operator, "MinArea", 0, 0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 9999999, 0);
        var minScore = GetDoubleParam(@operator, "MinScore", 0.0, 0.0, 1.0);
        var targetClassesRaw = GetStringParam(@operator, "TargetClasses", "");
        var targetClasses = targetClassesRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rx = GetIntParam(@operator, "RegionX", 0);
        var ry = GetIntParam(@operator, "RegionY", 0);
        var rw = GetIntParam(@operator, "RegionW", 0);
        var rh = GetIntParam(@operator, "RegionH", 0);
        var region = new Rect(rx, ry, Math.Max(0, rw), Math.Max(0, rh));

        var filtered = mode.ToLowerInvariant() switch
        {
            "area" => detections.Where(d => d.Area >= minArea && d.Area <= maxArea),
            "class" => targetClasses.Count == 0 ? detections : detections.Where(d => targetClasses.Contains(d.Label)),
            "region" => region.Width > 0 && region.Height > 0
                ? detections.Where(d => region.Contains(new Point((int)d.CenterX, (int)d.CenterY)))
                : Enumerable.Empty<DetectionResultValue>(),
            "score" => detections.Where(d => d.Confidence >= minScore),
            _ => detections
        };

        // Apply score threshold as a common post-filter if configured.
        if (minScore > 0 && !mode.Equals("score", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(d => d.Confidence >= minScore);
        }

        var resultDetections = filtered.ToList();
        var outputList = new DetectionListValue(resultDetections);

        if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                var resultImage = src.Clone();
                foreach (var d in resultDetections)
                {
                    var rect = new Rect((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
                    rect = ClampRect(rect, resultImage.Width, resultImage.Height);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    Cv2.Rectangle(resultImage, rect, new Scalar(0, 255, 0), 2);
                    Cv2.PutText(resultImage, $"{d.Label} {d.Confidence:P0}", new Point(rect.X, Math.Max(12, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 255, 0), 1);
                }

                if (region.Width > 0 && region.Height > 0)
                {
                    var drawRegion = ClampRect(region, resultImage.Width, resultImage.Height);
                    Cv2.Rectangle(resultImage, drawRegion, new Scalar(255, 200, 0), 1);
                }

                var output = CreateImageOutput(resultImage, new Dictionary<string, object>
                {
                    { "Detections", outputList },
                    { "Count", outputList.Count }
                });

                return Task.FromResult(OperatorExecutionOutput.Success(output));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Detections", outputList },
            { "Count", outputList.Count }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "FilterMode", "Area");
        var validModes = new[] { "Area", "Class", "Region", "Score" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("FilterMode must be Area/Class/Region/Score");
        }

        var minScore = GetDoubleParam(@operator, "MinScore", 0.0);
        if (minScore < 0 || minScore > 1)
        {
            return ValidationResult.Invalid("MinScore must be within [0, 1]");
        }

        return ValidationResult.Valid();
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, width - 1);
        var y = Math.Clamp(rect.Y, 0, height - 1);
        var w = Math.Clamp(rect.Width, 0, width - x);
        var h = Math.Clamp(rect.Height, 0, height - y);
        return new Rect(x, y, w, h);
    }

    private static bool TryParseDetectionList(object? obj, out List<DetectionResultValue> detections)
    {
        detections = new List<DetectionResultValue>();
        if (obj == null)
        {
            return false;
        }

        if (obj is DetectionListValue detectionList)
        {
            detections = detectionList.Detections.ToList();
            return true;
        }

        if (obj is IEnumerable<DetectionResultValue> typedSeq)
        {
            detections = typedSeq.ToList();
            return true;
        }

        if (obj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParseDetection(item, out var detection))
                {
                    detections.Add(detection);
                }
            }

            return detections.Count > 0;
        }

        return false;
    }

    private static bool TryParseDetection(object? obj, out DetectionResultValue detection)
    {
        detection = new DetectionResultValue();
        if (obj == null)
        {
            return false;
        }

        if (obj is DetectionResultValue typed)
        {
            detection = typed;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            if (!TryGetFloat(dict, "X", out var x) ||
                !TryGetFloat(dict, "Y", out var y) ||
                !TryGetFloat(dict, "Width", out var w) ||
                !TryGetFloat(dict, "Height", out var h))
            {
                return false;
            }

            TryGetFloat(dict, "Confidence", out var confidence);
            var label = TryGetString(dict, "Label", out var text) ? text : "object";
            detection = new DetectionResultValue(label, confidence, x, y, w, h);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0f, StringComparer.OrdinalIgnoreCase);
            return TryParseDetection(normalized, out detection);
        }

        return false;
    }

    private static bool TryGetFloat(IDictionary<string, object> dict, string key, out float value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            float f => (value = f) == f,
            double d => (value = (float)d) == (float)d,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => float.TryParse(raw.ToString(), out value)
        };
    }

    private static bool TryGetString(IDictionary<string, object> dict, string key, out string value)
    {
        value = string.Empty;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        value = raw.ToString() ?? string.Empty;
        return true;
    }
}

