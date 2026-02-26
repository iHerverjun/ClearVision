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
    DisplayName = "间隙测量",
    Description = "Measures spacing using points or image projection.",
    Category = "检测",
    IconName = "gap",
    Keywords = new[] { "gap", "spacing", "pitch", "distance" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[InputPort("Points", "Points", PortDataType.PointList, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Gaps", "Gaps", PortDataType.Any)]
[OutputPort("MeanGap", "Mean Gap", PortDataType.Float)]
[OutputPort("MinGap", "Min Gap", PortDataType.Float)]
[OutputPort("MaxGap", "Max Gap", PortDataType.Float)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("Direction", "Direction", "enum", DefaultValue = "Auto", Options = new[] { "Horizontal|Horizontal", "Vertical|Vertical", "Auto|Auto" })]
[OperatorParam("MinGap", "Min Gap", "double", DefaultValue = 0.0, Min = 0.0, Max = 1000000.0)]
[OperatorParam("MaxGap", "Max Gap", "double", DefaultValue = 0.0, Min = 0.0, Max = 1000000.0)]
[OperatorParam("ExpectedCount", "Expected Count", "int", DefaultValue = 0, Min = 0, Max = 10000)]
public class GapMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.GapMeasurement;

    public GapMeasurementOperator(ILogger<GapMeasurementOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var direction = GetStringParam(@operator, "Direction", "Auto");
        var minGapFilter = GetDoubleParam(@operator, "MinGap", 0.0, 0.0, 1_000_000);
        var maxGapFilter = GetDoubleParam(@operator, "MaxGap", 0.0, 0.0, 1_000_000);
        var expectedCount = GetIntParam(@operator, "ExpectedCount", 0, 0, 10_000);

        List<double> gaps;
        Mat? imageToDraw = null;

        if (TryGetPointList(inputs, out var points) && points.Count >= 2)
        {
            var useHorizontal = ResolveDirectionByPoints(direction, points);
            gaps = ComputeGapsFromPoints(points, useHorizontal);

            if (TryGetInputImage(inputs, out var sourceImage) && sourceImage != null)
            {
                var src = sourceImage.GetMat();
                if (!src.Empty())
                {
                    imageToDraw = src.Clone();
                    DrawPointGaps(imageToDraw, points, useHorizontal);
                }
            }
        }
        else if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (src.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
            }

            imageToDraw = src.Clone();
            gaps = ComputeGapsFromImage(src, direction, out var featurePositions, out var useHorizontal);
            DrawProjectionFeatures(imageToDraw, featurePositions, useHorizontal);
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Either Image or Points input is required"));
        }

        if (minGapFilter > 0)
        {
            gaps = gaps.Where(g => g >= minGapFilter).ToList();
        }

        if (maxGapFilter > 0)
        {
            gaps = gaps.Where(g => g <= maxGapFilter).ToList();
        }

        if (expectedCount > 0 && gaps.Count > expectedCount)
        {
            gaps = gaps.Take(expectedCount).ToList();
        }

        var count = gaps.Count;
        var mean = count > 0 ? gaps.Average() : 0.0;
        var min = count > 0 ? gaps.Min() : 0.0;
        var max = count > 0 ? gaps.Max() : 0.0;

        var outputData = new Dictionary<string, object>
        {
            { "Gaps", gaps },
            { "MeanGap", mean },
            { "MinGap", min },
            { "MaxGap", max },
            { "Count", count }
        };

        if (imageToDraw != null)
        {
            Cv2.PutText(
                imageToDraw,
                $"Gap Count:{count} Mean:{mean:F2}px",
                new Point(8, 24),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(255, 255, 255),
                2);

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(imageToDraw, outputData)));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var direction = GetStringParam(@operator, "Direction", "Auto");
        var validDirections = new[] { "Horizontal", "Vertical", "Auto" };
        if (!validDirections.Contains(direction, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Direction must be Horizontal, Vertical or Auto");
        }

        var minGap = GetDoubleParam(@operator, "MinGap", 0.0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 0.0);
        if (minGap < 0 || maxGap < 0)
        {
            return ValidationResult.Invalid("MinGap and MaxGap must be greater than or equal to 0");
        }

        if (maxGap > 0 && minGap > maxGap)
        {
            return ValidationResult.Invalid("MinGap cannot be greater than MaxGap");
        }

        return ValidationResult.Valid();
    }

    private static bool ResolveDirectionByPoints(string direction, IReadOnlyList<Position> points)
    {
        if (direction.Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (direction.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var xRange = points.Max(p => p.X) - points.Min(p => p.X);
        var yRange = points.Max(p => p.Y) - points.Min(p => p.Y);
        return xRange >= yRange;
    }

    private static List<double> ComputeGapsFromPoints(IReadOnlyList<Position> points, bool horizontal)
    {
        var sorted = horizontal
            ? points.OrderBy(p => p.X).ToList()
            : points.OrderBy(p => p.Y).ToList();

        var gaps = new List<double>(Math.Max(0, sorted.Count - 1));
        for (var i = 1; i < sorted.Count; i++)
        {
            var gap = horizontal
                ? sorted[i].X - sorted[i - 1].X
                : sorted[i].Y - sorted[i - 1].Y;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return gaps;
    }

    private static List<double> ComputeGapsFromImage(Mat src, string direction, out List<int> positions, out bool horizontal)
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

        var xProjection = GetProjection(gray, true);
        var yProjection = GetProjection(gray, false);

        if (direction.Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            horizontal = true;
        }
        else if (direction.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
        {
            horizontal = false;
        }
        else
        {
            horizontal = ComputeVariance(xProjection) >= ComputeVariance(yProjection);
        }

        var profile = horizontal ? xProjection : yProjection;
        positions = FindFeaturePositions(profile);

        var gaps = new List<double>(Math.Max(0, positions.Count - 1));
        for (var i = 1; i < positions.Count; i++)
        {
            var gap = positions[i] - positions[i - 1];
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return gaps;
    }

    private static double[] GetProjection(Mat gray, bool horizontal)
    {
        using var projection = new Mat();
        Cv2.Reduce(gray, projection, horizontal ? ReduceDimension.Row : ReduceDimension.Column, ReduceTypes.Avg, MatType.CV_64F);

        var length = horizontal ? projection.Cols : projection.Rows;
        var values = new double[length];

        for (var i = 0; i < length; i++)
        {
            values[i] = horizontal ? projection.At<double>(0, i) : projection.At<double>(i, 0);
        }

        return values;
    }

    private static double ComputeVariance(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            var diff = values[i] - mean;
            sum += diff * diff;
        }

        return sum / values.Count;
    }

    private static List<int> FindFeaturePositions(IReadOnlyList<double> profile)
    {
        var result = new List<int>();
        if (profile.Count < 3)
        {
            return result;
        }

        var mean = profile.Average();
        var std = Math.Sqrt(profile.Sum(v => (v - mean) * (v - mean)) / profile.Count);
        var threshold = mean + std * 0.5;

        for (var i = 1; i < profile.Count - 1; i++)
        {
            if (profile[i] < threshold)
            {
                continue;
            }

            if (profile[i] >= profile[i - 1] && profile[i] >= profile[i + 1])
            {
                if (result.Count == 0 || i - result[^1] > 3)
                {
                    result.Add(i);
                }
            }
        }

        return result;
    }

    private static void DrawProjectionFeatures(Mat image, IReadOnlyList<int> positions, bool horizontal)
    {
        foreach (var pos in positions)
        {
            if (horizontal)
            {
                Cv2.Line(image, new Point(pos, 0), new Point(pos, image.Rows - 1), new Scalar(0, 255, 255), 1);
            }
            else
            {
                Cv2.Line(image, new Point(0, pos), new Point(image.Cols - 1, pos), new Scalar(0, 255, 255), 1);
            }
        }
    }

    private static void DrawPointGaps(Mat image, IReadOnlyList<Position> points, bool horizontal)
    {
        foreach (var point in points)
        {
            Cv2.Circle(image, new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)), 3, new Scalar(0, 255, 0), -1);
        }

        var sorted = horizontal
            ? points.OrderBy(p => p.X).ToList()
            : points.OrderBy(p => p.Y).ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            var p1 = new Point((int)Math.Round(sorted[i - 1].X), (int)Math.Round(sorted[i - 1].Y));
            var p2 = new Point((int)Math.Round(sorted[i].X), (int)Math.Round(sorted[i].Y));
            Cv2.Line(image, p1, p2, new Scalar(255, 0, 0), 1);
        }
    }

    private static bool TryGetPointList(Dictionary<string, object>? inputs, out List<Position> points)
    {
        points = new List<Position>();

        if (inputs == null || !inputs.TryGetValue("Points", out var pointsObj) || pointsObj == null)
        {
            return false;
        }

        if (pointsObj is IEnumerable<Position> typed)
        {
            points = typed.ToList();
            return points.Count > 0;
        }

        if (pointsObj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParsePoint(item, out var point))
                {
                    points.Add(point);
                }
            }
        }

        return points.Count > 0;
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        if (obj is Position p)
        {
            point = p;
            return true;
        }

        if (obj is Point cvPoint)
        {
            point = new Position(cvPoint.X, cvPoint.Y);
            return true;
        }

        if (obj is Point2f cvPointF)
        {
            point = new Position(cvPointF.X, cvPointF.Y);
            return true;
        }

        if (obj is Point2d cvPointD)
        {
            point = new Position(cvPointD.X, cvPointD.Y);
            return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }
}
