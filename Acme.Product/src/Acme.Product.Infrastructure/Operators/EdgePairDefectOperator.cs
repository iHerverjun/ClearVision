// EdgePairDefectOperator.cs
// 双边缘缺陷检测算子
// 基于边缘对关系检测缺陷与异常
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

        if (!TryResolveLines(src, inputs, edgeMethod, expectedWidth, tolerance, out var line1, out var line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to resolve Line1/Line2 for edge-pair inspection"));
        }

        using var edgeMap = BuildEdgeMap(src, edgeMethod);
        var direction = new Point2d(line1.EndX - line1.StartX, line1.EndY - line1.StartY);
        var directionLength = Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y));
        if (directionLength <= 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Resolved Line1 is degenerate."));
        }

        var tangent = new Point2d(direction.X / directionLength, direction.Y / directionLength);
        var normal = new Point2d(-tangent.Y, tangent.X);
        var lineMidpointDelta = new Point2d(line2.MidX - line1.MidX, line2.MidY - line1.MidY);
        if ((lineMidpointDelta.X * normal.X) + (lineMidpointDelta.Y * normal.Y) < 0)
        {
            normal = new Point2d(-normal.X, -normal.Y);
        }

        var localSearchRadius = Math.Max(4, (int)Math.Ceiling(Math.Max(tolerance * 2.0, expectedWidth * 0.25)));
        var deviations = new List<double>(sampleCount);
        var defectPoints = new List<Point>();
        var defectSegmentCount = 0;
        var inDefectSegment = false;
        var maxDeviation = 0.0;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = sampleCount <= 1 ? 0.0 : (double)i / (sampleCount - 1);
            var predictedFirst = new Point2d(
                line1.StartX + (line1.EndX - line1.StartX) * t,
                line1.StartY + (line1.EndY - line1.StartY) * t);
            var predictedSecond = new Point2d(
                line2.StartX + (line2.EndX - line2.StartX) * t,
                line2.StartY + (line2.EndY - line2.StartY) * t);

            var firstPoint = TryFindLocalEdgePoint(edgeMap, predictedFirst, normal, localSearchRadius, out var localFirst)
                ? localFirst
                : predictedFirst;
            var secondPoint = TryFindLocalEdgePoint(edgeMap, predictedSecond, normal, localSearchRadius, out var localSecond)
                ? localSecond
                : predictedSecond;

            var width = Math.Sqrt(
                ((secondPoint.X - firstPoint.X) * (secondPoint.X - firstPoint.X)) +
                ((secondPoint.Y - firstPoint.Y) * (secondPoint.Y - firstPoint.Y)));
            var deviation = width - expectedWidth;
            deviations.Add(deviation);

            var abs = Math.Abs(deviation);
            if (abs > maxDeviation)
            {
                maxDeviation = abs;
            }

            if (abs > tolerance)
            {
                defectPoints.Add(new Point(
                    (int)Math.Round((firstPoint.X + secondPoint.X) * 0.5),
                    (int)Math.Round((firstPoint.Y + secondPoint.Y) * 0.5)));
                if (!inDefectSegment)
                {
                    defectSegmentCount++;
                    inDefectSegment = true;
                }
            }
            else
            {
                inDefectSegment = false;
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
            $"Defects:{defectSegmentCount} MaxDev:{maxDeviation:F2}",
            new Point(8, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);

        var output = new Dictionary<string, object>
        {
            { "DefectCount", defectSegmentCount },
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

    private static bool TryResolveLines(
        Mat src,
        Dictionary<string, object>? inputs,
        string edgeMethod,
        double expectedWidth,
        double tolerance,
        out LineData line1,
        out LineData line2)
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

        return TryDetectLinesFromImage(src, edgeMethod, expectedWidth, tolerance, out line1, out line2);
    }

    private static bool TryDetectLinesFromImage(
        Mat src,
        string edgeMethod,
        double expectedWidth,
        double tolerance,
        out LineData line1,
        out LineData line2)
    {
        line1 = new LineData();
        line2 = new LineData();

        using var edge = BuildEdgeMap(src, edgeMethod);

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

                var widthDelta = Math.Abs(separation - expectedWidth);
                var widthPenalty = widthDelta * (10.0 / (tolerance + 1.0));
                var withinToleranceBonus = widthDelta <= tolerance ? 60.0 : 0.0;
                var score = a.Length + b.Length - widthPenalty + withinToleranceBonus;
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

    private static Mat BuildEdgeMap(Mat src, string edgeMethod)
    {
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var edge = new Mat();
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

        return edge;
    }

    private static bool TryFindLocalEdgePoint(
        Mat edgeMap,
        Point2d predictedPoint,
        Point2d normal,
        int searchRadius,
        out Point2d edgePoint)
    {
        edgePoint = default;

        for (var delta = 0; delta <= searchRadius; delta++)
        {
            foreach (var signedDelta in EnumerateSignedOffsets(delta))
            {
                var sampleX = predictedPoint.X + (normal.X * signedDelta);
                var sampleY = predictedPoint.Y + (normal.Y * signedDelta);
                var roundedX = (int)Math.Round(sampleX);
                var roundedY = (int)Math.Round(sampleY);

                var hitCount = 0;
                var sumX = 0.0;
                var sumY = 0.0;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    var candidateY = roundedY + offsetY;
                    if (candidateY < 0 || candidateY >= edgeMap.Rows)
                    {
                        continue;
                    }

                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        var candidateX = roundedX + offsetX;
                        if (candidateX < 0 || candidateX >= edgeMap.Cols || edgeMap.At<byte>(candidateY, candidateX) == 0)
                        {
                            continue;
                        }

                        hitCount++;
                        sumX += candidateX;
                        sumY += candidateY;
                    }
                }

                if (hitCount > 0)
                {
                    edgePoint = new Point2d(sumX / hitCount, sumY / hitCount);
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<int> EnumerateSignedOffsets(int delta)
    {
        yield return delta;
        if (delta != 0)
        {
            yield return -delta;
        }
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

