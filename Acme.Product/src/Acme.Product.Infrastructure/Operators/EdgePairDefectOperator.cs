using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "边缘对缺陷",
    Description = "Checks edge-pair spacing deviations against expected width.",
    Category = "AI检测",
    IconName = "edge-pair-defect",
    Keywords = new[] { "edge pair", "notch", "bump", "deviation" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Line1", "Line 1", PortDataType.LineData, IsRequired = false)]
[InputPort("Line2", "Line 2", PortDataType.LineData, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("DefectCount", "Defect Count", PortDataType.Integer)]
[OutputPort("MaxDeviation", "Max Deviation", PortDataType.Float)]
[OutputPort("Deviations", "Deviations", PortDataType.Any)]
[OperatorParam("ExpectedWidth", "Expected Width", "double", DefaultValue = 20.0, Min = 0.0, Max = 100000.0)]
[OperatorParam("Tolerance", "Tolerance", "double", DefaultValue = 2.0, Min = 0.0, Max = 100000.0)]
[OperatorParam("NumSamples", "Sample Count", "int", DefaultValue = 100, Min = 5, Max = 5000)]
[OperatorParam("EdgeMethod", "Edge Method", "enum", DefaultValue = "Canny", Options = new[] { "Canny|Canny", "Sobel|Sobel" })]
public class EdgePairDefectOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.EdgePairDefect;

    public EdgePairDefectOperator(ILogger<EdgePairDefectOperator> logger) : base(logger)
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

        var expectedWidth = GetDoubleParam(@operator, "ExpectedWidth", 20.0, 0.0, 100000.0);
        var tolerance = GetDoubleParam(@operator, "Tolerance", 2.0, 0.0, 100000.0);
        var sampleCount = GetIntParam(@operator, "NumSamples", 100, 5, 5000);
        var edgeMethod = GetStringParam(@operator, "EdgeMethod", "Canny");

        if (!TryResolveLines(src, inputs, edgeMethod, out var line1, out var line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to resolve Line1/Line2 for edge-pair inspection"));
        }

        var deviations = new List<double>(sampleCount);
        var defectPoints = new List<Point>();
        var maxDeviation = 0.0;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = sampleCount <= 1 ? 0.0 : (double)i / (sampleCount - 1);
            var x = line1.StartX + (line1.EndX - line1.StartX) * t;
            var y = line1.StartY + (line1.EndY - line1.StartY) * t;

            var width = DistancePointToLine(x, y, line2);
            var deviation = width - expectedWidth;
            deviations.Add(deviation);

            var abs = Math.Abs(deviation);
            if (abs > maxDeviation)
            {
                maxDeviation = abs;
            }

            if (abs > tolerance)
            {
                defectPoints.Add(new Point((int)Math.Round(x), (int)Math.Round(y)));
            }
        }

        var result = src.Clone();
        Cv2.Line(result, new Point((int)line1.StartX, (int)line1.StartY), new Point((int)line1.EndX, (int)line1.EndY), new Scalar(0, 255, 0), 2);
        Cv2.Line(result, new Point((int)line2.StartX, (int)line2.StartY), new Point((int)line2.EndX, (int)line2.EndY), new Scalar(255, 0, 0), 2);

        foreach (var p in defectPoints)
        {
            Cv2.Circle(result, p, 3, new Scalar(0, 0, 255), -1);
        }

        Cv2.PutText(
            result,
            $"Defects:{defectPoints.Count} MaxDev:{maxDeviation:F2}",
            new Point(8, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);

        var output = new Dictionary<string, object>
        {
            { "DefectCount", defectPoints.Count },
            { "MaxDeviation", maxDeviation },
            { "Deviations", deviations }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var expectedWidth = GetDoubleParam(@operator, "ExpectedWidth", 20.0);
        if (expectedWidth < 0)
        {
            return ValidationResult.Invalid("ExpectedWidth must be >= 0");
        }

        var tolerance = GetDoubleParam(@operator, "Tolerance", 2.0);
        if (tolerance < 0)
        {
            return ValidationResult.Invalid("Tolerance must be >= 0");
        }

        var edgeMethod = GetStringParam(@operator, "EdgeMethod", "Canny");
        var validMethods = new[] { "Canny", "Sobel" };
        if (!validMethods.Contains(edgeMethod, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("EdgeMethod must be Canny or Sobel");
        }

        return ValidationResult.Valid();
    }

    private static bool TryResolveLines(Mat src, Dictionary<string, object>? inputs, string edgeMethod, out LineData line1, out LineData line2)
    {
        line1 = new LineData();
        line2 = new LineData();

        if (inputs != null &&
            inputs.TryGetValue("Line1", out var line1Obj) &&
            inputs.TryGetValue("Line2", out var line2Obj) &&
            TryParseLine(line1Obj, out line1) &&
            TryParseLine(line2Obj, out line2))
        {
            return true;
        }

        return TryDetectLinesFromImage(src, edgeMethod, out line1, out line2);
    }

    private static bool TryDetectLinesFromImage(Mat src, string edgeMethod, out LineData line1, out LineData line2)
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

        using var edge = new Mat();
        if (edgeMethod.Equals("Sobel", StringComparison.OrdinalIgnoreCase))
        {
            using var gradX = new Mat();
            using var gradY = new Mat();
            using var absX = new Mat();
            using var absY = new Mat();
            Cv2.Sobel(gray, gradX, MatType.CV_16S, 1, 0);
            Cv2.Sobel(gray, gradY, MatType.CV_16S, 0, 1);
            Cv2.ConvertScaleAbs(gradX, absX);
            Cv2.ConvertScaleAbs(gradY, absY);
            Cv2.AddWeighted(absX, 0.5, absY, 0.5, 0, edge);
            Cv2.Threshold(edge, edge, 60, 255, ThresholdTypes.Binary);
        }
        else
        {
            Cv2.Canny(gray, edge, 60, 160);
        }

        var lines = Cv2.HoughLinesP(edge, 1, Math.PI / 180, 80, 60, 10);
        if (lines == null || lines.Length < 2)
        {
            return false;
        }

        var candidates = lines
            .Select(l => new LineData(l.P1.X, l.P1.Y, l.P2.X, l.P2.Y))
            .OrderByDescending(l => l.Length)
            .Take(30)
            .ToList();

        var bestScore = double.MinValue;
        (LineData, LineData)? bestPair = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];
                var angleDiff = AngleDiff(a.Angle, b.Angle);
                if (angleDiff > 8)
                {
                    continue;
                }

                var separation = DistancePointToLine(a.MidX, a.MidY, b);
                if (separation <= 1)
                {
                    continue;
                }

                var score = a.Length + b.Length + separation;
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

        line1 = bestPair.Value.Item1;
        line2 = bestPair.Value.Item2;
        return true;
    }

    private static double DistancePointToLine(double px, double py, LineData line)
    {
        var a = line.EndY - line.StartY;
        var b = line.StartX - line.EndX;
        var c = line.EndX * line.StartY - line.StartX * line.EndY;
        var denominator = Math.Sqrt(a * a + b * b);
        if (denominator < 1e-9)
        {
            return 0;
        }

        return Math.Abs(a * px + b * py + c) / denominator;
    }

    private static double AngleDiff(float a, float b)
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

    private static bool TryParseLine(object? raw, out LineData line)
    {
        line = new LineData();
        if (raw == null)
        {
            return false;
        }

        if (raw is LineData data)
        {
            line = data;
            return true;
        }

        if (raw is IDictionary<string, object> dict)
        {
            if (TryGetFloat(dict, "StartX", out var x1) &&
                TryGetFloat(dict, "StartY", out var y1) &&
                TryGetFloat(dict, "EndX", out var x2) &&
                TryGetFloat(dict, "EndY", out var y2))
            {
                line = new LineData(x1, y1, x2, y2);
                return true;
            }

            if (TryGetFloat(dict, "X1", out x1) &&
                TryGetFloat(dict, "Y1", out y1) &&
                TryGetFloat(dict, "X2", out x2) &&
                TryGetFloat(dict, "Y2", out y2))
            {
                line = new LineData(x1, y1, x2, y2);
                return true;
            }
        }

        if (raw is IDictionary legacy)
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
