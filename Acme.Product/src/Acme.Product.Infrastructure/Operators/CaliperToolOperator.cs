// CaliperToolOperator.cs
// 卡尺测量算子
// 基于边缘搜索执行宽度与位置测量
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Industrial caliper-like edge pair detector over a single scan line.
/// </summary>
[OperatorMeta(
    DisplayName = "卡尺工具",
    Description = "Detects edge pairs along a scan line and reports width.",
    Category = "检测",
    IconName = "caliper",
    Keywords = new[] { "caliper", "edge pair", "width", "distance", "edge" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("SearchRegion", "Search Region", PortDataType.Rectangle, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Width", "Width", PortDataType.Float)]
[OutputPort("EdgePairs", "Edge Pairs", PortDataType.PointList)]
[OutputPort("PairCount", "Pair Count", PortDataType.Integer)]
[OutputPort("PairDistances", "Pair Distances", PortDataType.Any)]
[OutputPort("AverageDistance", "Average Distance", PortDataType.Float)]
[OutputPort("DistanceStdDev", "Distance StdDev", PortDataType.Float)]
[OperatorParam("Direction", "Direction", "enum", DefaultValue = "Horizontal", Options = new[] { "Horizontal|Horizontal", "Vertical|Vertical", "Custom|Custom" })]
[OperatorParam("Angle", "Angle", "double", DefaultValue = 0.0, Min = -180.0, Max = 180.0)]
[OperatorParam("Polarity", "Polarity", "enum", DefaultValue = "Both", Options = new[] { "DarkToLight|DarkToLight", "LightToDark|LightToDark", "Both|Both" })]
[OperatorParam("EdgeThreshold", "Edge Threshold", "double", DefaultValue = 18.0, Min = 1.0, Max = 255.0)]
[OperatorParam("ExpectedCount", "Expected Count", "int", DefaultValue = 1, Min = 1, Max = 100)]
[OperatorParam("MeasureMode", "Measure Mode", "enum", DefaultValue = "edge_pairs", Options = new[] { "single_edge|single_edge", "edge_pairs|edge_pairs" })]
[OperatorParam("PairDirection", "Pair Direction", "enum", DefaultValue = "any", Options = new[] { "positive_to_negative|positive_to_negative", "negative_to_positive|negative_to_positive", "any|any" })]
[OperatorParam("SubpixelAccuracy", "Subpixel Accuracy", "bool", DefaultValue = false)]
[OperatorParam("SubPixelMode", "Sub Pixel Mode", "enum", DefaultValue = "gradient_centroid", Options = new[] { "gradient_centroid|gradient_centroid", "zernike|zernike" })]
public class CaliperToolOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CaliperTool;

    public CaliperToolOperator(ILogger<CaliperToolOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var direction = GetStringParam(@operator, "Direction", "Horizontal");
        var angleDeg = GetDoubleParam(@operator, "Angle", 0);
        var polarity = GetStringParam(@operator, "Polarity", "Both");
        var edgeThreshold = GetDoubleParam(@operator, "EdgeThreshold", 18.0, 1.0, 255.0);
        var expectedCount = GetIntParam(@operator, "ExpectedCount", 1, 1, 100);
        var measureMode = GetStringParam(@operator, "MeasureMode", "edge_pairs");
        var pairDirection = GetStringParam(@operator, "PairDirection", "any");
        var subpixel = GetBoolParam(@operator, "SubpixelAccuracy", false);
        var subPixelMode = GetStringParam(@operator, "SubPixelMode", "gradient_centroid");
        var subpixelDetector = subpixel ? new SubPixelEdgeDetector
        {
            EdgeThreshold = (byte)Math.Clamp(edgeThreshold, 1.0, 255.0)
        } : null;

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var roi = ParseSearchRect(inputs, gray.Width, gray.Height);
        var scan = BuildScanLine(roi, direction, angleDeg);
        var sampleCount = Math.Max((int)Math.Ceiling(scan.Length), 24);

        var samples = SampleIntensity(gray, scan.Start, scan.End, sampleCount);
        var candidates = DetectEdgesSigned(samples, edgeThreshold, polarity);
        var detectedEdges = new List<DetectedEdge>(candidates.Count);

        foreach (var edge in candidates)
        {
            var t = sampleCount <= 1 ? 0.0 : (double)edge.Index / (sampleCount - 1);
            var refinePolarity = edge.Polarity == EdgePolarity.DarkToLight ? "DarkToLight" : "LightToDark";
            var refinedT = subpixel
                ? RefineSubpixel(samples, edge.Index, t, sampleCount, edgeThreshold, refinePolarity, subPixelMode, subpixelDetector)
                : t;
            var x = scan.Start.X + (scan.End.X - scan.Start.X) * refinedT;
            var y = scan.Start.Y + (scan.End.Y - scan.Start.Y) * refinedT;
            detectedEdges.Add(new DetectedEdge(edge.Index, edge.Polarity, new Position(x, y)));
        }

        var pairs = measureMode.Equals("edge_pairs", StringComparison.OrdinalIgnoreCase)
            ? BuildEdgePairs(detectedEdges, pairDirection, expectedCount)
            : new List<(int First, int Second)>();

        var pairDistances = new List<double>(pairs.Count);
        var pairedEdgePoints = new List<Position>(pairs.Count * 2);

        foreach (var (first, second) in pairs)
        {
            var p1 = detectedEdges[first].Point;
            var p2 = detectedEdges[second].Point;
            var distance = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            pairDistances.Add(distance);
            pairedEdgePoints.Add(p1);
            pairedEdgePoints.Add(p2);
        }

        var averageDistance = pairDistances.Count > 0 ? pairDistances.Average() : 0.0;
        var variance = pairDistances.Count > 1
            ? pairDistances.Select(d => (d - averageDistance) * (d - averageDistance)).Sum() / (pairDistances.Count - 1)
            : 0.0;
        var distanceStdDev = Math.Sqrt(Math.Max(0, variance));
        var pairCount = pairDistances.Count;
        var widthValue = averageDistance;

        var resultImage = src.Clone();
        var drawEdges = BuildDrawEdgeList(detectedEdges, pairs, pairedEdgePoints, measureMode);
        DrawScanAndEdges(resultImage, scan, drawEdges, pairCount, widthValue);

        var edgePairsOutput = measureMode.Equals("edge_pairs", StringComparison.OrdinalIgnoreCase)
            ? pairedEdgePoints
            : detectedEdges.Select(e => e.Point).ToList();

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Width", widthValue },
            { "EdgePairs", edgePairsOutput },
            { "PairCount", pairCount },
            { "PairDistances", pairDistances },
            { "AverageDistance", averageDistance },
            { "DistanceStdDev", distanceStdDev }
        });
        // Override image width key with measured width to match operator output contract.
        output["Width"] = widthValue;

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var direction = GetStringParam(@operator, "Direction", "Horizontal");
        var validDirections = new[] { "Horizontal", "Vertical", "Custom" };
        if (!validDirections.Contains(direction, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Direction must be Horizontal, Vertical or Custom");
        }

        var polarity = GetStringParam(@operator, "Polarity", "Both");
        var validPolarity = new[] { "DarkToLight", "LightToDark", "Both" };
        if (!validPolarity.Contains(polarity, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Polarity must be DarkToLight, LightToDark or Both");
        }

        var measureMode = GetStringParam(@operator, "MeasureMode", "edge_pairs");
        var validMeasureModes = new[] { "single_edge", "edge_pairs" };
        if (!validMeasureModes.Contains(measureMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("MeasureMode must be single_edge or edge_pairs");
        }

        var pairDirection = GetStringParam(@operator, "PairDirection", "any");
        var validPairDirections = new[] { "positive_to_negative", "negative_to_positive", "any" };
        if (!validPairDirections.Contains(pairDirection, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("PairDirection must be positive_to_negative, negative_to_positive or any");
        }

        var subPixelMode = GetStringParam(@operator, "SubPixelMode", "gradient_centroid");
        var validModes = new[] { "gradient_centroid", "zernike" };
        if (!validModes.Contains(subPixelMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("SubPixelMode must be gradient_centroid or zernike");
        }

        return ValidationResult.Valid();
    }

    private static Rect ParseSearchRect(Dictionary<string, object>? inputs, int width, int height)
    {
        var fallback = new Rect(0, 0, width, height);
        if (inputs == null || !inputs.TryGetValue("SearchRegion", out var regionObj) || regionObj == null)
        {
            return fallback;
        }

        if (regionObj is Rect rect)
        {
            return ClampRect(rect, width, height);
        }

        if (regionObj is IDictionary<string, object> dict)
        {
            if (TryGetInt(dict, "X", out var x) &&
                TryGetInt(dict, "Y", out var y) &&
                TryGetInt(dict, "Width", out var w) &&
                TryGetInt(dict, "Height", out var h))
            {
                return ClampRect(new Rect(x, y, w, h), width, height);
            }
        }

        if (regionObj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0, StringComparer.OrdinalIgnoreCase);
            if (TryGetInt(normalized, "X", out var x) &&
                TryGetInt(normalized, "Y", out var y) &&
                TryGetInt(normalized, "Width", out var w) &&
                TryGetInt(normalized, "Height", out var h))
            {
                return ClampRect(new Rect(x, y, w, h), width, height);
            }
        }

        return fallback;
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, width - 1);
        var y = Math.Clamp(rect.Y, 0, height - 1);
        var w = Math.Clamp(rect.Width, 1, width - x);
        var h = Math.Clamp(rect.Height, 1, height - y);
        return new Rect(x, y, w, h);
    }

    private static (Point2d Start, Point2d End, double Length) BuildScanLine(Rect roi, string direction, double angleDeg)
    {
        var cx = roi.X + roi.Width / 2.0;
        var cy = roi.Y + roi.Height / 2.0;

        Point2d start;
        Point2d end;

        if (direction.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
        {
            start = new Point2d(cx, roi.Top);
            end = new Point2d(cx, roi.Bottom - 1);
        }
        else if (direction.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            var rad = angleDeg * Math.PI / 180.0;
            var halfLen = Math.Sqrt(roi.Width * roi.Width + roi.Height * roi.Height) / 2.0;
            var dx = Math.Cos(rad) * halfLen;
            var dy = Math.Sin(rad) * halfLen;
            start = new Point2d(cx - dx, cy - dy);
            end = new Point2d(cx + dx, cy + dy);
        }
        else
        {
            start = new Point2d(roi.Left, cy);
            end = new Point2d(roi.Right - 1, cy);
        }

        var length = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));
        return (start, end, length);
    }

    private static List<double> SampleIntensity(Mat gray, Point2d start, Point2d end, int count)
    {
        var samples = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0.0 : (double)i / (count - 1);
            var x = start.X + (end.X - start.X) * t;
            var y = start.Y + (end.Y - start.Y) * t;
            samples.Add(SampleGrayBilinear(gray, x, y));
        }

        return samples;
    }

    private static double SampleGrayBilinear(Mat gray, double x, double y)
    {
        var clampedX = Math.Clamp(x, 0.0, gray.Width - 1.0);
        var clampedY = Math.Clamp(y, 0.0, gray.Height - 1.0);
        var x0 = (int)Math.Floor(clampedX);
        var y0 = (int)Math.Floor(clampedY);
        var x1 = Math.Min(x0 + 1, gray.Width - 1);
        var y1 = Math.Min(y0 + 1, gray.Height - 1);
        var fx = clampedX - x0;
        var fy = clampedY - y0;

        var v00 = gray.At<byte>(y0, x0);
        var v10 = gray.At<byte>(y0, x1);
        var v01 = gray.At<byte>(y1, x0);
        var v11 = gray.At<byte>(y1, x1);

        var top = v00 + ((v10 - v00) * fx);
        var bottom = v01 + ((v11 - v01) * fx);
        return top + ((bottom - top) * fy);
    }

    private enum EdgePolarity
    {
        DarkToLight,
        LightToDark
    }

    private readonly record struct EdgeCandidate(int Index, EdgePolarity Polarity);

    private readonly record struct DetectedEdge(int Index, EdgePolarity Polarity, Position Point);

    private static List<EdgeCandidate> DetectEdgesSigned(IReadOnlyList<double> samples, double threshold, string polarity)
    {
        var edges = new List<EdgeCandidate>();

        for (var i = 1; i < samples.Count; i++)
        {
            var grad = samples[i] - samples[i - 1];

            if (grad >= threshold)
            {
                if (!polarity.Equals("LightToDark", StringComparison.OrdinalIgnoreCase))
                {
                    edges.Add(new EdgeCandidate(i, EdgePolarity.DarkToLight));
                }
                continue;
            }

            if (grad <= -threshold)
            {
                if (!polarity.Equals("DarkToLight", StringComparison.OrdinalIgnoreCase))
                {
                    edges.Add(new EdgeCandidate(i, EdgePolarity.LightToDark));
                }
            }
        }

        return edges;
    }

    private static List<(int First, int Second)> BuildEdgePairs(
        IReadOnlyList<DetectedEdge> edges,
        string pairDirection,
        int maxPairs)
    {
        var pairs = new List<(int First, int Second)>();
        if (maxPairs <= 0 || edges.Count < 2)
        {
            return pairs;
        }

        var normalizedDirection = pairDirection?.Trim().ToLowerInvariant() ?? "any";

        for (var i = 0; i < edges.Count - 1 && pairs.Count < maxPairs; i++)
        {
            var first = edges[i];
            var second = edges[i + 1];

            if (first.Polarity == second.Polarity)
            {
                continue;
            }

            if (!PairDirectionMatches(first.Polarity, second.Polarity, normalizedDirection))
            {
                continue;
            }

            pairs.Add((i, i + 1));
            i++; // Consume the second edge.
        }

        return pairs;
    }

    private static bool PairDirectionMatches(EdgePolarity first, EdgePolarity second, string pairDirection)
    {
        return pairDirection switch
        {
            "positive_to_negative" => first == EdgePolarity.DarkToLight && second == EdgePolarity.LightToDark,
            "negative_to_positive" => first == EdgePolarity.LightToDark && second == EdgePolarity.DarkToLight,
            "any" => true,
            _ => true
        };
    }

    private static List<Position> BuildDrawEdgeList(
        IReadOnlyList<DetectedEdge> detectedEdges,
        IReadOnlyList<(int First, int Second)> pairs,
        IReadOnlyList<Position> pairedEdgePoints,
        string measureMode)
    {
        var allEdges = detectedEdges.Select(e => e.Point).ToList();

        if (!measureMode.Equals("edge_pairs", StringComparison.OrdinalIgnoreCase) || pairs.Count == 0)
        {
            return allEdges;
        }

        var used = new bool[detectedEdges.Count];
        foreach (var (first, second) in pairs)
        {
            if (first >= 0 && first < used.Length)
            {
                used[first] = true;
            }

            if (second >= 0 && second < used.Length)
            {
                used[second] = true;
            }
        }

        var unpaired = new List<Position>(Math.Max(0, allEdges.Count - pairedEdgePoints.Count));
        for (var i = 0; i < detectedEdges.Count; i++)
        {
            if (!used[i])
            {
                unpaired.Add(detectedEdges[i].Point);
            }
        }

        var drawEdges = new List<Position>(pairedEdgePoints.Count + unpaired.Count);
        drawEdges.AddRange(pairedEdgePoints);
        drawEdges.AddRange(unpaired);
        return drawEdges;
    }

    private static double RefineSubpixel(
        IReadOnlyList<double> samples,
        int idx,
        double fallbackT,
        int sampleCount,
        double edgeThreshold,
        string polarity,
        string subPixelMode,
        SubPixelEdgeDetector? detector)
    {
        if (idx <= 0 || idx >= samples.Count - 1)
        {
            return fallbackT;
        }

        if (detector != null)
        {
            if (subPixelMode.Equals("zernike", StringComparison.OrdinalIgnoreCase) &&
                TryRefineSubpixelZernike(samples, idx, sampleCount, edgeThreshold, polarity, detector, out var refinedZernike))
            {
                return refinedZernike;
            }

            if (TryRefineSubpixelCentroid(samples, idx, sampleCount, edgeThreshold, polarity, detector, out var refinedCentroid))
            {
                return refinedCentroid;
            }
        }

        var g0 = samples[idx] - samples[idx - 1];
        var g1 = samples[idx + 1] - samples[idx];
        var denom = g0 - g1;
        if (Math.Abs(denom) < 1e-6)
        {
            return fallbackT;
        }

        var offset = 0.5 * (g0 + g1) / denom;
        var refinedIndex = idx + offset;
        return Math.Clamp(refinedIndex / Math.Max(sampleCount - 1, 1), 0.0, 1.0);
    }

    private static bool TryBuildGradientWindow(
        IReadOnlyList<double> samples,
        int edgeIndex,
        int sampleCount,
        string polarity,
        out float[] window,
        out int start)
    {
        window = Array.Empty<float>();
        start = 0;

        var gradientCount = samples.Count - 1;
        if (gradientCount < 3)
        {
            return false;
        }

        var gradientIndex = edgeIndex - 1;
        if (gradientIndex < 0 || gradientIndex >= gradientCount)
        {
            return false;
        }

        var windowRadius = Math.Clamp(sampleCount / 20, 2, 6);
        start = Math.Max(0, gradientIndex - windowRadius);
        var end = Math.Min(gradientCount - 1, gradientIndex + windowRadius);
        var length = end - start + 1;
        if (length < 3)
        {
            return false;
        }

        window = new float[length];
        for (var i = 0; i < length; i++)
        {
            var grad = samples[start + i + 1] - samples[start + i];
            grad = polarity switch
            {
                var p when p.Equals("DarkToLight", StringComparison.OrdinalIgnoreCase) => Math.Max(0.0, grad),
                var p when p.Equals("LightToDark", StringComparison.OrdinalIgnoreCase) => Math.Max(0.0, -grad),
                _ => Math.Abs(grad)
            };
            window[i] = (float)Math.Min(255.0, grad);
        }

        return true;
    }

    private static bool TryRefineSubpixelCentroid(
        IReadOnlyList<double> samples,
        int edgeIndex,
        int sampleCount,
        double edgeThreshold,
        string polarity,
        SubPixelEdgeDetector detector,
        out double refinedT)
    {
        refinedT = 0;
        if (!TryBuildGradientWindow(samples, edgeIndex, sampleCount, polarity, out var window, out var start))
        {
            return false;
        }

        using var lineProfile = Mat.FromArray(window);
        lineProfile.Reshape(1, 1);
        var threshold = (byte)Math.Clamp(edgeThreshold, 1.0, 255.0);
        var centroid = detector.DetectCentroid(lineProfile, threshold);
        if (centroid < 0)
        {
            return false;
        }

        var refinedGradientIndex = start + centroid;
        var refinedSampleIndex = refinedGradientIndex + 1;
        refinedT = Math.Clamp(refinedSampleIndex / Math.Max(sampleCount - 1, 1), 0.0, 1.0);
        return true;
    }

    private static bool TryRefineSubpixelZernike(
        IReadOnlyList<double> samples,
        int edgeIndex,
        int sampleCount,
        double edgeThreshold,
        string polarity,
        SubPixelEdgeDetector detector,
        out double refinedT)
    {
        refinedT = 0;
        if (!TryBuildGradientWindow(samples, edgeIndex, sampleCount, polarity, out var window, out var start))
        {
            return false;
        }

        using var lineProfile = Mat.FromArray(window);
        lineProfile.Reshape(1, 1);
        detector.EdgeThreshold = (byte)Math.Clamp(edgeThreshold, 1.0, 255.0);
        var zernike = detector.DetectZernike(lineProfile);
        if (zernike < 0)
        {
            return false;
        }

        var refinedGradientIndex = start + zernike;
        var refinedSampleIndex = refinedGradientIndex + 1;
        refinedT = Math.Clamp(refinedSampleIndex / Math.Max(sampleCount - 1, 1), 0.0, 1.0);
        return true;
    }

    private static void DrawScanAndEdges(Mat image, (Point2d Start, Point2d End, double Length) scan, IReadOnlyList<Position> edges, int pairCount, double width)
    {
        Cv2.Line(image,
            new Point((int)Math.Round(scan.Start.X), (int)Math.Round(scan.Start.Y)),
            new Point((int)Math.Round(scan.End.X), (int)Math.Round(scan.End.Y)),
            new Scalar(0, 255, 255),
            1);

        for (var i = 0; i < edges.Count; i++)
        {
            var p = edges[i];
            var color = i < pairCount * 2 ? new Scalar(0, 255, 0) : new Scalar(0, 165, 255);
            Cv2.Circle(image, new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)), 3, color, -1);
        }

        Cv2.PutText(
            image,
            $"Caliper Width: {width:F2}px | Pairs: {pairCount}",
            new Point(8, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);
    }

    private static bool TryGetInt(IDictionary<string, object> dict, string key, out int value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            int i => (value = i) == i,
            long l => (value = (int)l) == (int)l,
            float f => (value = (int)f) == (int)f,
            double d => (value = (int)d) == (int)d,
            _ => int.TryParse(raw.ToString(), out value)
        };
    }
}


