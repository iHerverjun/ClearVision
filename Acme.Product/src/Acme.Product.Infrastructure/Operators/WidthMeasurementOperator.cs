// WidthMeasurementOperator.cs
// 宽度测量算子
// 基于边缘或线段测量目标宽度参数
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Measures width between two approximately parallel edges/lines.
/// </summary>
[OperatorMeta(
    DisplayName = "宽度测量",
    Description = "Measures width between parallel edges or lines.",
    Category = "检测",
    IconName = "ruler",
    Keywords = new[] { "width", "thickness", "gap", "distance" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Line1", "Line 1", PortDataType.LineData, IsRequired = false)]
[InputPort("Line2", "Line 2", PortDataType.LineData, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Width", "Width", PortDataType.Float)]
[OutputPort("MinWidth", "Min Width", PortDataType.Float)]
[OutputPort("MaxWidth", "Max Width", PortDataType.Float)]
[OperatorParam("MeasureMode", "Measure Mode", "enum", DefaultValue = "AutoEdge", Options = new[] { "AutoEdge|AutoEdge", "ManualLines|ManualLines" })]
[OperatorParam("NumSamples", "Sample Count", "int", DefaultValue = 24, Min = 10, Max = 100)]
[OperatorParam("Direction", "Direction", "enum", DefaultValue = "Perpendicular", Options = new[] { "Perpendicular|Perpendicular", "Custom|Custom" })]
[OperatorParam("CustomAngle", "Custom Angle", "double", DefaultValue = 0.0, Min = -180.0, Max = 180.0)]
public class WidthMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.WidthMeasurement;

    public WidthMeasurementOperator(ILogger<WidthMeasurementOperator> logger) : base(logger)
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

        var mode = GetStringParam(@operator, "MeasureMode", "AutoEdge");
        var numSamples = GetIntParam(@operator, "NumSamples", 24, 10, 100);
        var direction = GetStringParam(@operator, "Direction", "Perpendicular");
        var customAngle = GetDoubleParam(@operator, "CustomAngle", 0.0, -180.0, 180.0);

        LineData line1;
        LineData line2;

        if (mode.Equals("ManualLines", StringComparison.OrdinalIgnoreCase))
        {
            if (inputs == null ||
                !inputs.TryGetValue("Line1", out var line1Obj) || !TryParseLine(line1Obj, out line1) ||
                !inputs.TryGetValue("Line2", out var line2Obj) || !TryParseLine(line2Obj, out line2))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("ManualLines mode requires Line1 and Line2 inputs"));
            }
        }
        else
        {
            if (!TryDetectParallelLines(src, out line1, out line2))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Unable to detect two parallel lines from image"));
            }
        }

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var measurements = BuildMeasurementSamples(gray, line1, line2, numSamples, direction, customAngle);
        if (measurements.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No valid width samples generated"));
        }

        var widths = measurements.Select(m => m.Width).ToList();

        var minWidth = widths.Min();
        var maxWidth = widths.Max();
        var meanWidth = widths.Average();
        var refinedSampleCount = measurements.Count(m => m.UsedSubpixel);

        var resultImage = src.Clone();
        DrawMeasurementOverlay(resultImage, line1, line2, measurements, meanWidth, minWidth, maxWidth);

        var output = CreateImageOutput(resultImage, "ImageWidth", "ImageHeight", new Dictionary<string, object>
        {
            { "Width", meanWidth },
            { "MinWidth", minWidth },
            { "MaxWidth", maxWidth },
            { "Direction", direction },
            { "RefinedSampleCount", refinedSampleCount },
            { "SampleCount", measurements.Count }
        });

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "MeasureMode", "AutoEdge");
        var validModes = new[] { "AutoEdge", "ManualLines" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("MeasureMode must be AutoEdge or ManualLines");
        }

        var samples = GetIntParam(@operator, "NumSamples", 24);
        if (samples < 10 || samples > 100)
        {
            return ValidationResult.Invalid("NumSamples must be within [10, 100]");
        }

        var direction = GetStringParam(@operator, "Direction", "Perpendicular");
        var validDirections = new[] { "Perpendicular", "Custom" };
        if (!validDirections.Contains(direction, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Direction must be Perpendicular or Custom");
        }

        return ValidationResult.Valid();
    }

    private static List<MeasurementSample> BuildMeasurementSamples(Mat gray, LineData line1, LineData line2, int numSamples, string direction, double customAngle)
    {
        var measurements = new List<MeasurementSample>(numSamples);
        for (var i = 0; i < numSamples; i++)
        {
            var t = numSamples <= 1 ? 0.0 : (double)i / (numSamples - 1);
            var referenceStart = new Position(
                line1.StartX + (line1.EndX - line1.StartX) * t,
                line1.StartY + (line1.EndY - line1.StartY) * t);

            Position referenceEnd;
            if (direction.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryIntersectRayWithLine(referenceStart.X, referenceStart.Y, customAngle, line2, out referenceEnd, out _))
                {
                    continue;
                }
            }
            else
            {
                referenceEnd = ProjectPointToLine(referenceStart.X, referenceStart.Y, line2);
            }

            var fallbackWidth = Distance(referenceStart, referenceEnd);
            if (fallbackWidth <= 1e-6)
            {
                continue;
            }

            if (TryMeasureWidthByCaliper(gray, referenceStart, referenceEnd, out var startEdge, out var endEdge))
            {
                measurements.Add(new MeasurementSample(referenceStart, referenceEnd, startEdge, endEdge, Distance(startEdge, endEdge), true));
            }
            else
            {
                measurements.Add(new MeasurementSample(referenceStart, referenceEnd, referenceStart, referenceEnd, fallbackWidth, false));
            }
        }

        return measurements;
    }

    private static bool TryDetectParallelLines(Mat src, out LineData line1, out LineData line2)
    {
        line1 = new LineData();
        line2 = new LineData();

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 60, 160);

        var segments = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 60, 50, 15);
        if (segments == null || segments.Length < 2)
        {
            return false;
        }

        var candidates = segments
            .Select(s => new LineData(s.P1.X, s.P1.Y, s.P2.X, s.P2.Y))
            .OrderByDescending(l => l.Length)
            .Take(24)
            .ToList();

        if (candidates.Count < 2)
        {
            return false;
        }

        var bestScore = double.MinValue;
        (LineData L1, LineData L2)? bestPair = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];
                var angleDiff = AngleDiffDeg(a.Angle, b.Angle);
                if (angleDiff > 10)
                {
                    continue;
                }

                var separation = DistancePointToLine(a.MidX, a.MidY, b);
                if (separation < 2)
                {
                    continue;
                }

                var score = a.Length + b.Length + separation * 0.5 - angleDiff * 10;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPair = (a, b);
                }
            }
        }

        if (!bestPair.HasValue)
        {
            return false;
        }

        line1 = bestPair.Value.L1;
        line2 = bestPair.Value.L2;
        return true;
    }

    private static double AngleDiffDeg(float a, float b)
    {
        var diff = Math.Abs(a - b);
        while (diff > 180)
        {
            diff -= 180;
        }

        if (diff > 90)
        {
            diff = 180 - diff;
        }

        return diff;
    }

    private static double DistancePointToLine(double px, double py, LineData line)
    {
        var a = line.EndY - line.StartY;
        var b = line.StartX - line.EndX;
        var c = line.EndX * line.StartY - line.StartX * line.EndY;
        var denom = Math.Sqrt(a * a + b * b);
        if (denom < 1e-9)
        {
            return 0;
        }

        return Math.Abs(a * px + b * py + c) / denom;
    }

    private static bool TryMeasureWidthByCaliper(Mat gray, Position start, Position end, out Position startEdge, out Position endEdge)
    {
        startEdge = start;
        endEdge = end;

        var segmentLength = Distance(start, end);
        if (segmentLength < 4)
        {
            return false;
        }

        var dirX = (end.X - start.X) / segmentLength;
        var dirY = (end.Y - start.Y) / segmentLength;
        var searchHalfWindow = Math.Clamp(segmentLength / 3.0, 6.0, 18.0);

        var startWindowStart = new Position(start.X - (dirX * searchHalfWindow), start.Y - (dirY * searchHalfWindow));
        var startWindowEnd = new Position(start.X + (dirX * searchHalfWindow), start.Y + (dirY * searchHalfWindow));
        var endWindowStart = new Position(end.X - (dirX * searchHalfWindow), end.Y - (dirY * searchHalfWindow));
        var endWindowEnd = new Position(end.X + (dirX * searchHalfWindow), end.Y + (dirY * searchHalfWindow));

        if (!TryFindLocalEdge(gray, startWindowStart, startWindowEnd, out startEdge))
        {
            return false;
        }

        if (!TryFindLocalEdge(gray, endWindowStart, endWindowEnd, out endEdge))
        {
            return false;
        }

        return Distance(startEdge, endEdge) > 1e-6;
    }

    private static bool TryFindLocalEdge(Mat gray, Position scanStart, Position scanEnd, out Position edge)
    {
        edge = scanStart;
        var scanLength = Distance(scanStart, scanEnd);
        if (scanLength < 2)
        {
            return false;
        }

        var sampleCount = Math.Max((int)Math.Ceiling(scanLength * 4), 24);
        var samples = SampleIntensity(gray, scanStart, scanEnd, sampleCount);
        if (samples.Count < 6)
        {
            return false;
        }

        var gradients = new double[samples.Count - 1];
        for (var i = 1; i < samples.Count; i++)
        {
            gradients[i - 1] = samples[i] - samples[i - 1];
        }

        var gradientIndex = FindPeakGradientIndex(gradients, 0, gradients.Length);
        if (gradientIndex < 0 || Math.Abs(gradients[gradientIndex]) < 3.0)
        {
            return false;
        }

        var edgeIndex = gradientIndex + 1;
        var edgeT = sampleCount <= 1 ? 0.0 : (double)edgeIndex / (sampleCount - 1);
        var refinedEdgeT = RefineSubpixel(samples, edgeIndex, edgeT, sampleCount);
        edge = Lerp(scanStart, scanEnd, refinedEdgeT);
        return true;
    }

    private static List<double> SampleIntensity(Mat gray, Position start, Position end, int count)
    {
        var samples = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0.0 : (double)i / (count - 1);
            var x = start.X + ((end.X - start.X) * t);
            var y = start.Y + ((end.Y - start.Y) * t);
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

    private static int FindPeakGradientIndex(IReadOnlyList<double> gradients, int startInclusive, int endExclusive)
    {
        var safeStart = Math.Clamp(startInclusive, 0, gradients.Count);
        var safeEnd = Math.Clamp(endExclusive, safeStart, gradients.Count);
        var bestIndex = -1;
        var bestStrength = double.MinValue;
        for (var i = safeStart; i < safeEnd; i++)
        {
            var strength = Math.Abs(gradients[i]);
            if (strength > bestStrength)
            {
                bestStrength = strength;
                bestIndex = i;
            }
        }

        return bestIndex;
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

    private static void DrawMeasurementOverlay(Mat image, LineData line1, LineData line2, IReadOnlyList<MeasurementSample> measurements, double mean, double min, double max)
    {
        Cv2.Line(image, new Point((int)line1.StartX, (int)line1.StartY), new Point((int)line1.EndX, (int)line1.EndY), new Scalar(0, 255, 0), 2);
        Cv2.Line(image, new Point((int)line2.StartX, (int)line2.StartY), new Point((int)line2.EndX, (int)line2.EndY), new Scalar(255, 200, 0), 2);

        var step = Math.Max(1, measurements.Count / 6);
        for (var i = 0; i < measurements.Count; i += step)
        {
            var sample = measurements[i];
            var start = sample.UsedSubpixel ? sample.StartEdge : sample.ReferenceStart;
            var end = sample.UsedSubpixel ? sample.EndEdge : sample.ReferenceEnd;
            var color = sample.UsedSubpixel ? new Scalar(0, 0, 255) : new Scalar(0, 165, 255);

            Cv2.Line(
                image,
                new Point((int)Math.Round(start.X), (int)Math.Round(start.Y)),
                new Point((int)Math.Round(end.X), (int)Math.Round(end.Y)),
                color,
                1);

            Cv2.Circle(image, new Point((int)Math.Round(start.X), (int)Math.Round(start.Y)), 2, color, -1);
            Cv2.Circle(image, new Point((int)Math.Round(end.X), (int)Math.Round(end.Y)), 2, color, -1);
        }

        Cv2.PutText(
            image,
            $"Width Mean:{mean:F2}px Min:{min:F2}px Max:{max:F2}px",
            new Point(8, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);
    }

    private static Position Lerp(Position start, Position end, double t)
    {
        return new Position(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }

    private static double Distance(Position start, Position end)
    {
        return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    }

    private static Position ProjectPointToLine(double px, double py, LineData line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        var norm2 = dx * dx + dy * dy;
        if (norm2 < 1e-9)
        {
            return new Position(line.StartX, line.StartY);
        }

        var t = ((px - line.StartX) * dx + (py - line.StartY) * dy) / norm2;
        return new Position(line.StartX + t * dx, line.StartY + t * dy);
    }

    private static bool TryIntersectRayWithLine(double px, double py, double angleDeg, LineData line, out Position intersection, out double distance)
    {
        var angleRad = angleDeg * Math.PI / 180.0;
        var rayDx = Math.Cos(angleRad);
        var rayDy = Math.Sin(angleRad);
        var lineDx = line.EndX - line.StartX;
        var lineDy = line.EndY - line.StartY;

        var denominator = (rayDx * lineDy) - (rayDy * lineDx);
        if (Math.Abs(denominator) < 1e-9)
        {
            intersection = new Position(px, py);
            distance = 0;
            return false;
        }

        var deltaX = line.StartX - px;
        var deltaY = line.StartY - py;
        var rayT = ((deltaX * lineDy) - (deltaY * lineDx)) / denominator;
        if (rayT < 0)
        {
            intersection = new Position(px, py);
            distance = 0;
            return false;
        }

        intersection = new Position(px + (rayT * rayDx), py + (rayT * rayDy));
        distance = Math.Sqrt(Math.Pow(intersection.X - px, 2) + Math.Pow(intersection.Y - py, 2));
        return true;
    }

    private static bool TryParseLine(object? obj, out LineData line)
    {
        line = new LineData();
        if (obj == null)
            return false;

        if (obj is LineData lineData)
        {
            line = lineData;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            if (TryGetFloat(dict, "StartX", out var sx) &&
                TryGetFloat(dict, "StartY", out var sy) &&
                TryGetFloat(dict, "EndX", out var ex) &&
                TryGetFloat(dict, "EndY", out var ey))
            {
                line = new LineData(sx, sy, ex, ey);
                return true;
            }

            if (TryGetFloat(dict, "X1", out sx) &&
                TryGetFloat(dict, "Y1", out sy) &&
                TryGetFloat(dict, "X2", out ex) &&
                TryGetFloat(dict, "Y2", out ey))
            {
                line = new LineData(sx, sy, ex, ey);
                return true;
            }
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0f, StringComparer.OrdinalIgnoreCase);
            return TryParseLine(normalized, out line);
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

    private sealed record MeasurementSample(
        Position ReferenceStart,
        Position ReferenceEnd,
        Position StartEdge,
        Position EndEdge,
        double Width,
        bool UsedSubpixel);
}


