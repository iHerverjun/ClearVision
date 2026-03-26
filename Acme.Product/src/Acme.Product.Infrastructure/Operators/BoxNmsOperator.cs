// BoxNmsOperator.cs
// 非极大值抑制算子
// 对候选框执行 NMS 去重并保留最优结果
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
/// Non-maximum suppression for detection boxes.
/// </summary>
[OperatorMeta(
    DisplayName = "候选框抑制",
    Description = "Runs non-maximum suppression on detection boxes.",
    Category = "数据处理",
    IconName = "nms",
    Keywords = new[] { "nms", "box", "iou", "suppression" }
)]
[InputPort("Detections", "Detections", PortDataType.DetectionList, IsRequired = true)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[InputPort("SourceImage", "Source Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Detections", "Detections", PortDataType.DetectionList)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("InputCount", "Input Count", PortDataType.Integer)]
[OutputPort("SuppressedCount", "Suppressed Count", PortDataType.Integer)]
[OutputPort("SuppressedDetections", "Suppressed Detections", PortDataType.DetectionList)]
[OutputPort("Diagnostics", "Diagnostics", PortDataType.Any)]
[OperatorParam("IouThreshold", "IoU Threshold", "double", DefaultValue = 0.45, Min = 0.1, Max = 1.0)]
[OperatorParam("ScoreThreshold", "Score Threshold", "double", DefaultValue = 0.25, Min = 0.0, Max = 1.0)]
[OperatorParam("MaxDetections", "Max Detections", "int", DefaultValue = 100, Min = 1, Max = 1000)]
[OperatorParam("ShowSuppressed", "Show Suppressed", "bool", DefaultValue = true)]
public class BoxNmsOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BoxNms;

    public BoxNmsOperator(ILogger<BoxNmsOperator> logger) : base(logger)
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

        var iouThreshold = GetDoubleParam(@operator, "IouThreshold", 0.45, 0.1, 1.0);
        var scoreThreshold = GetDoubleParam(@operator, "ScoreThreshold", 0.25, 0.0, 1.0);
        var maxDetections = GetIntParam(@operator, "MaxDetections", 100, 1, 1000);
        var showSuppressed = GetBoolParam(@operator, "ShowSuppressed", true);
        var inputCount = detections.Count;

        var candidates = detections
            .Where(d => d.Confidence >= scoreThreshold)
            .OrderByDescending(d => d.Confidence)
            .ToList();

        var kept = new List<DetectionResultValue>();
        var suppressed = new HashSet<DetectionResultValue>();

        foreach (var group in candidates.GroupBy(d => d.Label ?? string.Empty))
        {
            var groupCandidates = group.OrderByDescending(d => d.Confidence).ToList();
            var removed = new bool[groupCandidates.Count];

            for (var i = 0; i < groupCandidates.Count; i++)
            {
                if (removed[i])
                {
                    continue;
                }

                var current = groupCandidates[i];
                kept.Add(current);

                for (var j = i + 1; j < groupCandidates.Count; j++)
                {
                    if (removed[j])
                    {
                        continue;
                    }

                    var iou = IoU(current, groupCandidates[j]);
                    if (iou > iouThreshold)
                    {
                        removed[j] = true;
                        suppressed.Add(groupCandidates[j]);
                    }
                }
            }
        }

        if (kept.Count > maxDetections)
        {
            var orderedKept = kept.OrderByDescending(d => d.Confidence).ToList();
            foreach (var truncatedDetection in orderedKept.Skip(maxDetections))
            {
                suppressed.Add(truncatedDetection);
            }

            kept = orderedKept.Take(maxDetections).ToList();
        }

        var outputDetections = new DetectionListValue(kept);
        var suppressedDetections = new DetectionListValue(suppressed.OrderByDescending(d => d.Confidence).ToList());
        var diagnostics = CreateDiagnostics(
            inputCount,
            candidates.Count,
            outputDetections,
            suppressedDetections,
            iouThreshold,
            scoreThreshold,
            maxDetections);

        // Try to use SourceImage first (clean image), fallback to Image (may have previous drawings)
        var imageToUse = TryGetInputImage(inputs, "SourceImage", out var sourceImageWrapper) && sourceImageWrapper != null
            ? sourceImageWrapper
            : (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null ? imageWrapper : null);
        
        if (imageToUse != null)
        {
            var src = imageToUse.GetMat();
            if (!src.Empty())
            {
                var resultImage = src.Clone();
                if (showSuppressed)
                {
                    DrawDetections(resultImage, suppressed, new Scalar(0, 0, 255), 1, "S");
                }

                DrawDetections(resultImage, kept, new Scalar(0, 255, 0), 2, "K");

                var output = CreateImageOutput(resultImage, new Dictionary<string, object>
                {
                    { "Detections", outputDetections },
                    { "Count", outputDetections.Count },
                    { "InputCount", inputCount },
                    { "SuppressedCount", suppressedDetections.Count },
                    { "SuppressedDetections", suppressedDetections },
                    { "Diagnostics", diagnostics }
                });
                return Task.FromResult(OperatorExecutionOutput.Success(output));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Detections", outputDetections },
            { "Count", outputDetections.Count },
            { "InputCount", inputCount },
            { "SuppressedCount", suppressedDetections.Count },
            { "SuppressedDetections", suppressedDetections },
            { "Diagnostics", diagnostics }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var iouThreshold = GetDoubleParam(@operator, "IouThreshold", 0.45);
        if (iouThreshold < 0.1 || iouThreshold > 1.0)
        {
            return ValidationResult.Invalid("IouThreshold must be in [0.1, 1.0]");
        }

        var scoreThreshold = GetDoubleParam(@operator, "ScoreThreshold", 0.25);
        if (scoreThreshold < 0 || scoreThreshold > 1.0)
        {
            return ValidationResult.Invalid("ScoreThreshold must be in [0, 1.0]");
        }

        return ValidationResult.Valid();
    }

    private static float IoU(DetectionResultValue a, DetectionResultValue b)
    {
        var ax1 = a.X;
        var ay1 = a.Y;
        var ax2 = a.X + a.Width;
        var ay2 = a.Y + a.Height;

        var bx1 = b.X;
        var by1 = b.Y;
        var bx2 = b.X + b.Width;
        var by2 = b.Y + b.Height;

        var xx1 = Math.Max(ax1, bx1);
        var yy1 = Math.Max(ay1, by1);
        var xx2 = Math.Min(ax2, bx2);
        var yy2 = Math.Min(ay2, by2);

        var interW = Math.Max(0, xx2 - xx1);
        var interH = Math.Max(0, yy2 - yy1);
        var inter = interW * interH;

        var areaA = Math.Max(0, a.Width) * Math.Max(0, a.Height);
        var areaB = Math.Max(0, b.Width) * Math.Max(0, b.Height);
        var union = areaA + areaB - inter;
        if (union <= 0)
        {
            return 0;
        }

        return inter / union;
    }

    private static void DrawDetections(Mat image, IEnumerable<DetectionResultValue> detections, Scalar color, int thickness, string tag)
    {
        foreach (var d in detections)
        {
            var rect = new Rect((int)Math.Round(d.X), (int)Math.Round(d.Y), (int)Math.Round(d.Width), (int)Math.Round(d.Height));
            rect = ClampRect(rect, image.Width, image.Height);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            Cv2.Rectangle(image, rect, color, thickness);
            var text = $"{tag}:{d.Label} {d.Confidence:P0}";
            Cv2.PutText(image, text, new Point(rect.X, Math.Max(12, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.4, color, 1);
        }
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
                if (TryParseDetection(item, out var d))
                {
                    detections.Add(d);
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

        if (obj is DetectionResultValue det)
        {
            detection = det;
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

            TryGetFloat(dict, "Confidence", out var conf);
            var label = TryGetString(dict, "Label", out var lbl) ? lbl : "object";
            detection = new DetectionResultValue(label, conf, x, y, w, h);
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

    private static Dictionary<string, object> CreateDiagnostics(
        int inputCount,
        int candidateCount,
        DetectionListValue keptDetections,
        DetectionListValue suppressedDetections,
        double iouThreshold,
        double scoreThreshold,
        int maxDetections)
    {
        return new Dictionary<string, object>
        {
            ["InputCount"] = inputCount,
            ["CandidateCount"] = candidateCount,
            ["KeptCount"] = keptDetections.Count,
            ["SuppressedCount"] = suppressedDetections.Count,
            ["IouThreshold"] = iouThreshold,
            ["ScoreThreshold"] = scoreThreshold,
            ["MaxDetections"] = maxDetections,
            ["KeptDetections"] = keptDetections,
            ["SuppressedDetections"] = suppressedDetections
        };
    }
}


