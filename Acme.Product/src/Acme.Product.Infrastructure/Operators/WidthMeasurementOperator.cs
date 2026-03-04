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

        var widths = SampleWidths(line1, line2, numSamples);
        if (widths.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No valid width samples generated"));
        }

        var minWidth = widths.Min();
        var maxWidth = widths.Max();
        var meanWidth = widths.Average();

        var resultImage = src.Clone();
        DrawMeasurementOverlay(resultImage, line1, line2, widths, numSamples, meanWidth, minWidth, maxWidth);

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Width", meanWidth },
            { "MinWidth", minWidth },
            { "MaxWidth", maxWidth }
        });
        // Override image width key with measured width to match operator output contract.
        output["Width"] = meanWidth;

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

        return ValidationResult.Valid();
    }

    private static List<double> SampleWidths(LineData line1, LineData line2, int numSamples)
    {
        var widths = new List<double>(numSamples);
        for (var i = 0; i < numSamples; i++)
        {
            var t = numSamples <= 1 ? 0.0 : (double)i / (numSamples - 1);
            var x = line1.StartX + (line1.EndX - line1.StartX) * t;
            var y = line1.StartY + (line1.EndY - line1.StartY) * t;
            widths.Add(DistancePointToLine(x, y, line2));
        }

        return widths;
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

    private static void DrawMeasurementOverlay(Mat image, LineData line1, LineData line2, IReadOnlyList<double> widths, int numSamples, double mean, double min, double max)
    {
        Cv2.Line(image, new Point((int)line1.StartX, (int)line1.StartY), new Point((int)line1.EndX, (int)line1.EndY), new Scalar(0, 255, 0), 2);
        Cv2.Line(image, new Point((int)line2.StartX, (int)line2.StartY), new Point((int)line2.EndX, (int)line2.EndY), new Scalar(255, 200, 0), 2);

        var step = Math.Max(1, numSamples / 6);
        for (var i = 0; i < numSamples; i += step)
        {
            var t = numSamples <= 1 ? 0.0 : (double)i / (numSamples - 1);
            var x = line1.StartX + (line1.EndX - line1.StartX) * t;
            var y = line1.StartY + (line1.EndY - line1.StartY) * t;

            // perpendicular projection to line2
            var proj = ProjectPointToLine(x, y, line2);
            Cv2.Line(
                image,
                new Point((int)Math.Round(x), (int)Math.Round(y)),
                new Point((int)Math.Round(proj.X), (int)Math.Round(proj.Y)),
                new Scalar(0, 0, 255),
                1);
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
}


