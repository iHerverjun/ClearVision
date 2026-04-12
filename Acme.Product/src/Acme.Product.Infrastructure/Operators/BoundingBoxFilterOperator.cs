// BoundingBoxFilterOperator.cs
// 边界框过滤算子
// 按面积、类别、置信度等条件过滤检测框
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using DetectionListValue = Acme.Product.Core.ValueObjects.DetectionList;
using DetectionResultValue = Acme.Product.Core.ValueObjects.DetectionResult;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Filters detection boxes by area/class/region/score.
/// </summary>
[OperatorMeta(
    DisplayName = "候选框过滤 (Bounding Box)",
    Description = "Filters detections by area, class, region, or score.",
    Category = "数据处理",
    IconName = "filter",
    Keywords = new[] { "bounding box filter", "detection filter", "class filter", "area filter", "score" }
)]
[InputPort("Detections", "Detections", PortDataType.DetectionList, IsRequired = true)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Detections", "Detections", PortDataType.DetectionList)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("FilterMode", "Filter Mode", "enum", DefaultValue = "Area", Options = new[] { "Area|Area", "Class|Class", "Region|Region", "Score|Score" })]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 9999999, Min = 0)]
[OperatorParam("TargetClasses", "Target Classes", "string", DefaultValue = "")]
[OperatorParam("MinScore", "Min Score", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("RegionX", "Region X", "int", DefaultValue = 0)]
[OperatorParam("RegionY", "Region Y", "int", DefaultValue = 0)]
[OperatorParam("RegionW", "Region Width", "int", DefaultValue = 0)]
[OperatorParam("RegionH", "Region Height", "int", DefaultValue = 0)]
public class BoundingBoxFilterOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BoxFilter;

    public BoundingBoxFilterOperator(ILogger<BoundingBoxFilterOperator> logger) : base(logger)
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
        var normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("area" or "class" or "region" or "score"))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FilterMode must be Area/Class/Region/Score"));
        }

        var filtered = normalizedMode switch
        {
            "area" => detections.Where(d => d.Area >= minArea && d.Area <= maxArea),
            "class" => targetClasses.Count == 0 ? detections : detections.Where(d => targetClasses.Contains(d.Label)),
            "region" => region.Width > 0 && region.Height > 0
                ? detections.Where(d => IsCenterInsideRegion(d, region))
                : Enumerable.Empty<DetectionResultValue>(),
            "score" => detections.Where(d => d.Confidence >= minScore),
            _ => Enumerable.Empty<DetectionResultValue>()
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
                var incomingVisualizationDetections = BuildVisualizationDetections(detections, minScore);
                var keptVisualizationDetections = BuildVisualizationDetections(resultDetections, minScore);
                var resultImage = src.Clone();
                DrawDetections(resultImage, incomingVisualizationDetections, new Scalar(255, 120, 0), 1, "IN");

                if (region.Width > 0 && region.Height > 0)
                {
                    var drawRegion = ClampRect(region, resultImage.Width, resultImage.Height);
                    Cv2.Rectangle(resultImage, drawRegion, new Scalar(255, 200, 0), 1);
                }

                DrawDetections(resultImage, keptVisualizationDetections, new Scalar(0, 255, 0), 2, "KEEP");

                var output = CreateImageOutput(resultImage, new Dictionary<string, object>
                {
                    { "Detections", outputList },
                    { "ReceivedCount", detections.Count },
                    { "Count", outputList.Count },
                    { "ReceivedVisualizationCount", incomingVisualizationDetections.Count },
                    { "VisualizationCount", keptVisualizationDetections.Count }
                });

                return Task.FromResult(OperatorExecutionOutput.Success(output));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Detections", outputList },
            { "ReceivedCount", detections.Count },
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

        var minArea = GetDoubleParam(@operator, "MinArea", 0, 0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 9999999, 0);
        if (minArea > maxArea)
        {
            return ValidationResult.Invalid("MinArea must be less than or equal to MaxArea");
        }

        return ValidationResult.Valid();
    }

    private static bool IsCenterInsideRegion(DetectionResultValue detection, Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return false;
        }

        var left = region.X;
        var top = region.Y;
        var right = region.X + region.Width;
        var bottom = region.Y + region.Height;

        var centerX = detection.CenterX;
        var centerY = detection.CenterY;
        return centerX >= left && centerY >= top && centerX < right && centerY < bottom;
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, width - 1);
        var y = Math.Clamp(rect.Y, 0, height - 1);
        var w = Math.Clamp(rect.Width, 0, width - x);
        var h = Math.Clamp(rect.Height, 0, height - y);
        return new Rect(x, y, w, h);
    }

    private static void DrawDetections(
        Mat image,
        IEnumerable<DetectionResultValue> detections,
        Scalar color,
        int thickness,
        string tag)
    {
        foreach (var detection in detections)
        {
            var rect = new Rect((int)Math.Round(detection.X), (int)Math.Round(detection.Y), (int)Math.Round(detection.Width), (int)Math.Round(detection.Height));
            rect = ClampRect(rect, image.Width, image.Height);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            Cv2.Rectangle(image, rect, color, thickness);
            var text = $"{tag}:{detection.Label} {detection.Confidence:P0}";
            Cv2.PutText(image, text, new Point(rect.X, Math.Max(12, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.4, color, 1);
        }
    }

    private static List<DetectionResultValue> BuildVisualizationDetections(
        List<DetectionResultValue> detections,
        double minScore)
    {
        if (detections.Count == 0)
        {
            return detections;
        }

        var scoreFloor = Math.Max((float)minScore, 0.25f);
        var filtered = detections
            .Where(detection => detection.Confidence >= scoreFloor)
            .ToList();
        if (filtered.Count == 0)
        {
            filtered = detections;
        }

        var kept = new List<DetectionResultValue>();
        foreach (var group in filtered.GroupBy(detection => detection.Label ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var groupCandidates = group
                .OrderByDescending(detection => detection.Confidence)
                .ToList();
            var removed = new bool[groupCandidates.Count];

            for (var i = 0; i < groupCandidates.Count; i++)
            {
                if (removed[i])
                {
                    continue;
                }

                kept.Add(groupCandidates[i]);
                for (var j = i + 1; j < groupCandidates.Count; j++)
                {
                    if (removed[j])
                    {
                        continue;
                    }

                    if (IoU(groupCandidates[i], groupCandidates[j]) > 0.45f)
                    {
                        removed[j] = true;
                    }
                }
            }
        }

        return kept;
    }

    private static float IoU(DetectionResultValue a, DetectionResultValue b)
    {
        var xx1 = Math.Max(a.X, b.X);
        var yy1 = Math.Max(a.Y, b.Y);
        var xx2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var yy2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        var interW = Math.Max(0, xx2 - xx1);
        var interH = Math.Max(0, yy2 - yy1);
        var inter = interW * interH;

        var union = Math.Max(0, a.Width) * Math.Max(0, a.Height)
            + Math.Max(0, b.Width) * Math.Max(0, b.Height)
            - inter;
        return union <= 0 ? 0 : inter / union;
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
            var hasAnyItem = false;
            foreach (var item in enumerable)
            {
                hasAnyItem = true;
                if (TryParseDetection(item, out var detection))
                {
                    detections.Add(detection);
                }
            }

            return !hasAnyItem || detections.Count > 0;
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


