using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Industrial caliper-like edge pair detector over a single scan line.
/// </summary>
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
        var subpixel = GetBoolParam(@operator, "SubpixelAccuracy", false);

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
        var edgeIndices = DetectEdges(samples, edgeThreshold, polarity);
        var edgePoints = new List<Position>(edgeIndices.Count);

        foreach (var idx in edgeIndices)
        {
            var t = sampleCount <= 1 ? 0.0 : (double)idx / (sampleCount - 1);
            var refinedT = subpixel ? RefineSubpixel(samples, idx, t, sampleCount) : t;
            var x = scan.Start.X + (scan.End.X - scan.Start.X) * refinedT;
            var y = scan.Start.Y + (scan.End.Y - scan.Start.Y) * refinedT;
            edgePoints.Add(new Position(x, y));
        }

        var pairWidths = new List<double>();
        var pairCount = Math.Min(expectedCount, edgePoints.Count / 2);
        for (var i = 0; i < pairCount; i++)
        {
            var p1 = edgePoints[i * 2];
            var p2 = edgePoints[i * 2 + 1];
            var width = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            pairWidths.Add(width);
        }

        var widthValue = pairWidths.Count > 0 ? pairWidths.Average() : 0.0;

        var resultImage = src.Clone();
        DrawScanAndEdges(resultImage, scan, edgePoints, pairCount, widthValue);

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Width", widthValue },
            { "EdgePairs", edgePoints },
            { "PairCount", pairCount }
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

            var ix = Math.Clamp((int)Math.Round(x), 0, gray.Width - 1);
            var iy = Math.Clamp((int)Math.Round(y), 0, gray.Height - 1);
            samples.Add(gray.At<byte>(iy, ix));
        }

        return samples;
    }

    private static List<int> DetectEdges(IReadOnlyList<double> samples, double threshold, string polarity)
    {
        var edges = new List<int>();

        for (var i = 1; i < samples.Count; i++)
        {
            var grad = samples[i] - samples[i - 1];
            var pass = polarity switch
            {
                var p when p.Equals("DarkToLight", StringComparison.OrdinalIgnoreCase) => grad >= threshold,
                var p when p.Equals("LightToDark", StringComparison.OrdinalIgnoreCase) => grad <= -threshold,
                _ => Math.Abs(grad) >= threshold
            };

            if (pass)
            {
                edges.Add(i);
            }
        }

        return edges;
    }

    private static double RefineSubpixel(IReadOnlyList<double> samples, int idx, double fallbackT, int sampleCount)
    {
        if (idx <= 0 || idx >= samples.Count - 1)
        {
            return fallbackT;
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

