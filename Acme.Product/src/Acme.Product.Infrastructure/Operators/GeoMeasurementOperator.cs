// GeoMeasurementOperator.cs
// 几何测量算子
// 计算点线圆等几何元素之间的距离与角度
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "几何测量",
    Description = "General geometry measurement between point/line/circle elements.",
    Category = "检测",
    IconName = "geometry",
    Keywords = new[] { "geometry", "point-line", "line-circle", "circle-circle" }
)]
[InputPort("Element1", "Element 1", PortDataType.Any, IsRequired = true)]
[InputPort("Element2", "Element 2", PortDataType.Any, IsRequired = true)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("Intersection1", "Intersection 1", PortDataType.Point)]
[OutputPort("Intersection2", "Intersection 2", PortDataType.Point)]
[OutputPort("MeasureType", "Measure Type", PortDataType.String)]
[OperatorParam("Element1Type", "Element1 Type", "enum", DefaultValue = "Auto", Options = new[] { "Auto|Auto", "Point|Point", "Line|Line", "Circle|Circle" })]
[OperatorParam("Element2Type", "Element2 Type", "enum", DefaultValue = "Auto", Options = new[] { "Auto|Auto", "Point|Point", "Line|Line", "Circle|Circle" })]
[OperatorParam("DistanceModel", "Distance Model", "enum", DefaultValue = "InfiniteLine", Options = new[] { "InfiniteLine|Infinite line", "Segment|Segment" })]
public class GeoMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.GeoMeasurement;

    public GeoMeasurementOperator(ILogger<GeoMeasurementOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null ||
            !inputs.TryGetValue("Element1", out var element1Obj) ||
            !inputs.TryGetValue("Element2", out var element2Obj))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Element1 and Element2 are required"));
        }

        var type1 = ResolveType(element1Obj, GetStringParam(@operator, "Element1Type", "Auto"));
        var type2 = ResolveType(element2Obj, GetStringParam(@operator, "Element2Type", "Auto"));

        if (type1 == GeoElementType.Unknown || type2 == GeoElementType.Unknown)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to resolve element types"));
        }

        var distanceModelText = GetStringParam(@operator, "DistanceModel", "InfiniteLine");
        if (!TryParseDistanceModel(distanceModelText, out var distanceModel))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DistanceModel must be InfiniteLine or Segment"));
        }

        var distance = 0.0;
        var angle = 0.0;
        var intersection1 = new Position(0, 0);
        var intersection2 = new Position(0, 0);
        var measureType = $"{type1}-{type2}";

        if (type1 == GeoElementType.Point && type2 == GeoElementType.Point &&
            TryParsePoint(element1Obj, out var p1) &&
            TryParsePoint(element2Obj, out var p2))
        {
            distance = Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }
        else if (type1 == GeoElementType.Point && type2 == GeoElementType.Line &&
                 TryParsePoint(element1Obj, out var p) &&
                 TryParseLine(element2Obj, out var line))
        {
            distance = DistancePointToLine(p.X, p.Y, line);
        }
        else if (type1 == GeoElementType.Line && type2 == GeoElementType.Point &&
                 TryParseLine(element1Obj, out line) &&
                 TryParsePoint(element2Obj, out p))
        {
            distance = DistancePointToLine(p.X, p.Y, line);
            measureType = "Point-Line";
        }
        else if (type1 == GeoElementType.Point && type2 == GeoElementType.Circle &&
                 TryParsePoint(element1Obj, out p) &&
                 TryParseCircle(element2Obj, out var circle))
        {
            var centerDist = Math.Sqrt((p.X - circle.CenterX) * (p.X - circle.CenterX) + (p.Y - circle.CenterY) * (p.Y - circle.CenterY));
            distance = Math.Abs(centerDist - circle.Radius);
        }
        else if (type1 == GeoElementType.Circle && type2 == GeoElementType.Point &&
                 TryParseCircle(element1Obj, out circle) &&
                 TryParsePoint(element2Obj, out p))
        {
            var centerDist = Math.Sqrt((p.X - circle.CenterX) * (p.X - circle.CenterX) + (p.Y - circle.CenterY) * (p.Y - circle.CenterY));
            distance = Math.Abs(centerDist - circle.Radius);
            measureType = "Point-Circle";
        }
        else if (type1 == GeoElementType.Line && type2 == GeoElementType.Line &&
                 TryParseLine(element1Obj, out var line1) &&
                 TryParseLine(element2Obj, out var line2))
        {
            angle = AngleBetweenLines(line1, line2);
            var hasIntersection = TryGetLineIntersection(line1, line2, out var cross);
            if (hasIntersection)
            {
                intersection1 = cross;
            }

            distance = distanceModel switch
            {
                DistanceModel.InfiniteLine => DistanceLineToLineInfinite(line1, line2, hasIntersection),
                DistanceModel.Segment => DistanceSegmentToSegment(line1, line2),
                _ => 0
            };
        }
        else if (type1 == GeoElementType.Line && type2 == GeoElementType.Circle &&
                 TryParseLine(element1Obj, out line1) &&
                 TryParseCircle(element2Obj, out circle))
        {
            var intersects = SolveLineCircleIntersections(line1, circle);
            if (intersects.Count > 0)
            {
                intersection1 = intersects[0];
            }

            if (intersects.Count > 1)
            {
                intersection2 = intersects[1];
            }

            distance = DistancePointToLine(circle.CenterX, circle.CenterY, line1);
        }
        else if (type1 == GeoElementType.Circle && type2 == GeoElementType.Line &&
                 TryParseCircle(element1Obj, out circle) &&
                 TryParseLine(element2Obj, out line1))
        {
            var intersects = SolveLineCircleIntersections(line1, circle);
            if (intersects.Count > 0)
            {
                intersection1 = intersects[0];
            }

            if (intersects.Count > 1)
            {
                intersection2 = intersects[1];
            }

            distance = DistancePointToLine(circle.CenterX, circle.CenterY, line1);
            measureType = "Line-Circle";
        }
        else if (type1 == GeoElementType.Circle && type2 == GeoElementType.Circle &&
                 TryParseCircle(element1Obj, out var c1) &&
                 TryParseCircle(element2Obj, out var c2))
        {
            var dx = c2.CenterX - c1.CenterX;
            var dy = c2.CenterY - c1.CenterY;
            var centerDistance = Math.Sqrt(dx * dx + dy * dy);
            distance = Math.Max(0.0, centerDistance - c1.Radius - c2.Radius);
            var intersects = SolveCircleCircleIntersections(c1, c2);
            if (intersects.Count > 0)
            {
                intersection1 = intersects[0];
            }

            if (intersects.Count > 1)
            {
                intersection2 = intersects[1];
            }
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Unsupported geometry type combination"));
        }

        var output = new Dictionary<string, object>
        {
            { "Distance", distance },
            { "Angle", angle },
            { "Intersection1", intersection1 },
            { "Intersection2", intersection2 },
            { "MeasureType", measureType },
            { "DistanceModel", distanceModel.ToString() },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var valid = new[] { "Auto", "Point", "Line", "Circle" };

        var type1 = GetStringParam(@operator, "Element1Type", "Auto");
        if (!valid.Contains(type1, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Element1Type must be Auto, Point, Line, or Circle");
        }

        var type2 = GetStringParam(@operator, "Element2Type", "Auto");
        if (!valid.Contains(type2, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Element2Type must be Auto, Point, Line, or Circle");
        }

        var distanceModel = GetStringParam(@operator, "DistanceModel", "InfiniteLine");
        if (!TryParseDistanceModel(distanceModel, out _))
        {
            return ValidationResult.Invalid("DistanceModel must be InfiniteLine or Segment");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParseDistanceModel(string model, out DistanceModel parsed)
    {
        parsed = DistanceModel.InfiniteLine;
        if (string.Equals(model, "InfiniteLine", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.InfiniteLine;
            return true;
        }

        if (string.Equals(model, "Segment", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.Segment;
            return true;
        }

        return false;
    }

    private static GeoElementType ResolveType(object raw, string preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && !preferred.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return preferred.ToLowerInvariant() switch
            {
                "point" => GeoElementType.Point,
                "line" => GeoElementType.Line,
                "circle" => GeoElementType.Circle,
                _ => GeoElementType.Unknown
            };
        }

        if (TryParsePoint(raw, out _))
        {
            return GeoElementType.Point;
        }

        if (TryParseLine(raw, out _))
        {
            return GeoElementType.Line;
        }

        return TryParseCircle(raw, out _) ? GeoElementType.Circle : GeoElementType.Unknown;
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

    private static double DistanceLineToLineInfinite(LineData line1, LineData line2, bool hasIntersection)
    {
        if (hasIntersection)
        {
            return 0.0;
        }

        return DistancePointToLine(line1.StartX, line1.StartY, line2);
    }

    private static double DistanceSegmentToSegment(LineData line1, LineData line2)
    {
        if (TryGetSegmentIntersection(line1, line2, out _))
        {
            return 0.0;
        }

        var d1 = DistancePointToSegment(line1.StartX, line1.StartY, line2);
        var d2 = DistancePointToSegment(line1.EndX, line1.EndY, line2);
        var d3 = DistancePointToSegment(line2.StartX, line2.StartY, line1);
        var d4 = DistancePointToSegment(line2.EndX, line2.EndY, line1);
        return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
    }

    private static double DistancePointToSegment(double px, double py, LineData line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        var norm2 = dx * dx + dy * dy;
        if (norm2 < 1e-9)
        {
            return Math.Sqrt((px - line.StartX) * (px - line.StartX) + (py - line.StartY) * (py - line.StartY));
        }

        var t = ((px - line.StartX) * dx + (py - line.StartY) * dy) / norm2;
        t = Math.Clamp(t, 0.0, 1.0);
        var projX = line.StartX + t * dx;
        var projY = line.StartY + t * dy;
        var ex = px - projX;
        var ey = py - projY;
        return Math.Sqrt(ex * ex + ey * ey);
    }

    private static bool TryGetSegmentIntersection(LineData l1, LineData l2, out Position intersection)
    {
        intersection = new Position(0, 0);
        if (!TryGetLineIntersection(l1, l2, out var cross))
        {
            return false;
        }

        if (IsPointOnSegment(cross, l1) && IsPointOnSegment(cross, l2))
        {
            intersection = cross;
            return true;
        }

        return false;
    }

    private static bool IsPointOnSegment(Position p, LineData line)
    {
        var minX = Math.Min(line.StartX, line.EndX) - 1e-6;
        var maxX = Math.Max(line.StartX, line.EndX) + 1e-6;
        var minY = Math.Min(line.StartY, line.EndY) - 1e-6;
        var maxY = Math.Max(line.StartY, line.EndY) + 1e-6;
        return p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
    }

    private static double AngleBetweenLines(LineData l1, LineData l2)
    {
        var v1x = l1.EndX - l1.StartX;
        var v1y = l1.EndY - l1.StartY;
        var v2x = l2.EndX - l2.StartX;
        var v2y = l2.EndY - l2.StartY;

        var norm1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var norm2 = Math.Sqrt(v2x * v2x + v2y * v2y);
        if (norm1 < 1e-9 || norm2 < 1e-9)
        {
            return 0;
        }

        var cos = Math.Clamp((v1x * v2x + v1y * v2y) / (norm1 * norm2), -1.0, 1.0);
        var angle = Math.Acos(cos) * 180.0 / Math.PI;
        return angle > 90 ? 180 - angle : angle;
    }

    private static bool TryGetLineIntersection(LineData l1, LineData l2, out Position point)
    {
        point = new Position(0, 0);

        var denominator = (l1.StartX - l1.EndX) * (l2.StartY - l2.EndY) -
                          (l1.StartY - l1.EndY) * (l2.StartX - l2.EndX);
        if (Math.Abs(denominator) < 1e-9)
        {
            return false;
        }

        var pxNumerator = (l1.StartX * l1.EndY - l1.StartY * l1.EndX) * (l2.StartX - l2.EndX) -
                          (l1.StartX - l1.EndX) * (l2.StartX * l2.EndY - l2.StartY * l2.EndX);
        var pyNumerator = (l1.StartX * l1.EndY - l1.StartY * l1.EndX) * (l2.StartY - l2.EndY) -
                          (l1.StartY - l1.EndY) * (l2.StartX * l2.EndY - l2.StartY * l2.EndX);
        point = new Position(pxNumerator / denominator, pyNumerator / denominator);
        return true;
    }

    private static List<Position> SolveLineCircleIntersections(LineData line, CircleSpec circle)
    {
        var result = new List<Position>();

        var x1 = line.StartX - circle.CenterX;
        var y1 = line.StartY - circle.CenterY;
        var x2 = line.EndX - circle.CenterX;
        var y2 = line.EndY - circle.CenterY;
        var dx = x2 - x1;
        var dy = y2 - y1;

        var a = dx * dx + dy * dy;
        var b = 2 * (x1 * dx + y1 * dy);
        var c = x1 * x1 + y1 * y1 - circle.Radius * circle.Radius;

        var delta = b * b - 4 * a * c;
        if (delta < 0 || Math.Abs(a) < 1e-12)
        {
            return result;
        }

        var sqrt = Math.Sqrt(Math.Max(0, delta));
        var t1 = (-b + sqrt) / (2 * a);
        var t2 = (-b - sqrt) / (2 * a);

        result.Add(new Position(line.StartX + t1 * (line.EndX - line.StartX), line.StartY + t1 * (line.EndY - line.StartY)));
        if (Math.Abs(t1 - t2) > 1e-9)
        {
            result.Add(new Position(line.StartX + t2 * (line.EndX - line.StartX), line.StartY + t2 * (line.EndY - line.StartY)));
        }

        return result;
    }

    private static List<Position> SolveCircleCircleIntersections(CircleSpec c1, CircleSpec c2)
    {
        var result = new List<Position>();
        var dx = c2.CenterX - c1.CenterX;
        var dy = c2.CenterY - c1.CenterY;
        var d = Math.Sqrt(dx * dx + dy * dy);

        if (d < 1e-9 || d > c1.Radius + c2.Radius || d < Math.Abs(c1.Radius - c2.Radius))
        {
            return result;
        }

        var a = (c1.Radius * c1.Radius - c2.Radius * c2.Radius + d * d) / (2 * d);
        var h2 = c1.Radius * c1.Radius - a * a;
        if (h2 < 0)
        {
            return result;
        }

        var h = Math.Sqrt(Math.Max(0, h2));
        var xm = c1.CenterX + a * dx / d;
        var ym = c1.CenterY + a * dy / d;
        var rx = -dy * (h / d);
        var ry = dx * (h / d);

        result.Add(new Position(xm + rx, ym + ry));
        if (h > 1e-9)
        {
            result.Add(new Position(xm - rx, ym - ry));
        }

        return result;
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

        if (raw is IDictionary<string, object> dict &&
            TryGetFloat(dict, "StartX", out var x1) &&
            TryGetFloat(dict, "StartY", out var y1) &&
            TryGetFloat(dict, "EndX", out var x2) &&
            TryGetFloat(dict, "EndY", out var y2))
        {
            line = new LineData(x1, y1, x2, y2);
            return true;
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

    private static bool TryParseCircle(object? raw, out CircleSpec circle)
    {
        circle = new CircleSpec(0, 0, 0);
        if (raw == null)
        {
            return false;
        }

        if (raw is CircleData data)
        {
            circle = new CircleSpec(data.CenterX, data.CenterY, data.Radius);
            return true;
        }

        if (raw is IDictionary<string, object> dict)
        {
            if (TryGetDouble(dict, "CenterX", out var cx) &&
                TryGetDouble(dict, "CenterY", out var cy) &&
                TryGetDouble(dict, "Radius", out var radius))
            {
                circle = new CircleSpec(cx, cy, radius);
                return true;
            }

            if (TryGetDouble(dict, "X", out cx) &&
                TryGetDouble(dict, "Y", out cy) &&
                TryGetDouble(dict, "R", out radius))
            {
                circle = new CircleSpec(cx, cy, radius);
                return true;
            }
        }

        if (raw is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParseCircle(normalized, out circle);
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

    private enum GeoElementType
    {
        Unknown = 0,
        Point,
        Line,
        Circle
    }

    private enum DistanceModel
    {
        InfiniteLine = 0,
        Segment = 1
    }

    private sealed record CircleSpec(double CenterX, double CenterY, double Radius);
}
