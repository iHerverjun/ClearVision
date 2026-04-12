using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

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
[OperatorParam("DistanceModel", "Distance Model", "enum", DefaultValue = "Segment", Options = new[] { "Segment|Segment", "InfiniteLine|Infinite line" })]
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

        if (!TryParseDistanceModel(GetStringParam(@operator, "DistanceModel", "Segment"), out var distanceModel))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DistanceModel must be Segment or InfiniteLine"));
        }

        if (!TryMeasure(element1Obj, element2Obj, type1, type2, distanceModel, out var measurement, out var error))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(error ?? "Unsupported geometry type combination"));
        }

        var output = new Dictionary<string, object>
        {
            { "Distance", measurement.Distance },
            { "Angle", measurement.Angle },
            { "Intersection1", measurement.Intersection1 },
            { "Intersection2", measurement.Intersection2 },
            { "MeasureType", measurement.MeasureType },
            { "DistanceModel", distanceModel.ToString() },
            { "DistanceUnit", "Pixel" },
            { "DistanceMeaning", measurement.DistanceMeaning },
            { "Relation", measurement.Relation },
            { "IntersectionCount", measurement.IntersectionCount },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var validTypes = new[] { "Auto", "Point", "Line", "Circle" };
        var type1 = GetStringParam(@operator, "Element1Type", "Auto");
        if (!validTypes.Contains(type1, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Element1Type must be Auto, Point, Line, or Circle");
        }

        var type2 = GetStringParam(@operator, "Element2Type", "Auto");
        if (!validTypes.Contains(type2, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Element2Type must be Auto, Point, Line, or Circle");
        }

        var distanceModel = GetStringParam(@operator, "DistanceModel", "Segment");
        if (!TryParseDistanceModel(distanceModel, out _))
        {
            return ValidationResult.Invalid("DistanceModel must be Segment or InfiniteLine");
        }

        return ValidationResult.Valid();
    }

    private static bool TryMeasure(
        object element1,
        object element2,
        GeoElementType type1,
        GeoElementType type2,
        DistanceModel distanceModel,
        out GeometryMeasurement measurement,
        out string? error)
    {
        measurement = default;
        error = null;

        if (type1 == GeoElementType.Point && type2 == GeoElementType.Point &&
            TryParsePoint(element1, out var point1) &&
            TryParsePoint(element2, out var point2))
        {
            measurement = new GeometryMeasurement(
                Distance: MeasurementGeometryHelper.Distance(point1, point2),
                Angle: 0.0,
                Intersection1: MeasurementGeometryHelper.NoIntersection,
                Intersection2: MeasurementGeometryHelper.NoIntersection,
                MeasureType: "Point-Point",
                DistanceMeaning: "CenterDistance",
                Relation: "Separated",
                IntersectionCount: 0);
            return true;
        }

        if (type1 == GeoElementType.Point && type2 == GeoElementType.Line &&
            TryParsePoint(element1, out var point) &&
            TryParseLine(element2, out var line))
        {
            measurement = CreatePointLineMeasurement(point, line, distanceModel);
            return true;
        }

        if (type1 == GeoElementType.Line && type2 == GeoElementType.Point &&
            TryParseLine(element1, out line) &&
            TryParsePoint(element2, out point))
        {
            measurement = CreatePointLineMeasurement(point, line, distanceModel) with { MeasureType = "Point-Line" };
            return true;
        }

        if (type1 == GeoElementType.Point && type2 == GeoElementType.Circle &&
            TryParsePoint(element1, out point) &&
            TryParseCircle(element2, out var circle))
        {
            var centerDistance = MeasurementGeometryHelper.Distance(point.X, point.Y, circle.CenterX, circle.CenterY);
            measurement = new GeometryMeasurement(
                Distance: Math.Abs(centerDistance - circle.Radius),
                Angle: 0.0,
                Intersection1: MeasurementGeometryHelper.NoIntersection,
                Intersection2: MeasurementGeometryHelper.NoIntersection,
                MeasureType: "Point-Circle",
                DistanceMeaning: "BoundaryGap",
                Relation: centerDistance < circle.Radius ? "Inside" : "Outside",
                IntersectionCount: 0);
            return true;
        }

        if (type1 == GeoElementType.Circle && type2 == GeoElementType.Point &&
            TryParseCircle(element1, out circle) &&
            TryParsePoint(element2, out point))
        {
            var centerDistance = MeasurementGeometryHelper.Distance(point.X, point.Y, circle.CenterX, circle.CenterY);
            measurement = new GeometryMeasurement(
                Distance: Math.Abs(centerDistance - circle.Radius),
                Angle: 0.0,
                Intersection1: MeasurementGeometryHelper.NoIntersection,
                Intersection2: MeasurementGeometryHelper.NoIntersection,
                MeasureType: "Point-Circle",
                DistanceMeaning: "BoundaryGap",
                Relation: centerDistance < circle.Radius ? "Inside" : "Outside",
                IntersectionCount: 0);
            return true;
        }

        if (type1 == GeoElementType.Line && type2 == GeoElementType.Line &&
            TryParseLine(element1, out var line1) &&
            TryParseLine(element2, out var line2))
        {
            var infiniteIntersection = MeasurementGeometryHelper.TryGetInfiniteLineIntersection(line1, line2, out var cross)
                ? cross
                : MeasurementGeometryHelper.NoIntersection;
            var segmentIntersection = MeasurementGeometryHelper.TryGetSegmentIntersection(line1, line2, out var segmentCross)
                ? segmentCross
                : MeasurementGeometryHelper.NoIntersection;
            var hasSegmentIntersection = !double.IsNaN(segmentIntersection.X);
            var distance = distanceModel == DistanceModel.Segment
                ? MeasurementGeometryHelper.DistanceSegmentToSegment(line1, line2)
                : (!double.IsNaN(infiniteIntersection.X) ? 0.0 : MeasurementGeometryHelper.DistancePointToInfiniteLine(line1.StartX, line1.StartY, line2));

            measurement = new GeometryMeasurement(
                Distance: distance,
                Angle: MeasurementGeometryHelper.AngleBetweenLineDirections(line1, line2),
                Intersection1: distanceModel == DistanceModel.Segment ? segmentIntersection : infiniteIntersection,
                Intersection2: MeasurementGeometryHelper.NoIntersection,
                MeasureType: "Line-Line",
                DistanceMeaning: distanceModel == DistanceModel.Segment ? "SegmentShortestDistance" : "InfiniteLineShortestDistance",
                Relation: hasSegmentIntersection ? "Intersecting" : "Separated",
                IntersectionCount: distanceModel == DistanceModel.Segment
                    ? (hasSegmentIntersection ? 1 : 0)
                    : (double.IsNaN(infiniteIntersection.X) ? 0 : 1));
            return true;
        }

        if (type1 == GeoElementType.Line && type2 == GeoElementType.Circle &&
            TryParseLine(element1, out line1) &&
            TryParseCircle(element2, out circle))
        {
            measurement = CreateLineCircleMeasurement(line1, circle, distanceModel);
            return true;
        }

        if (type1 == GeoElementType.Circle && type2 == GeoElementType.Line &&
            TryParseCircle(element1, out circle) &&
            TryParseLine(element2, out line1))
        {
            measurement = CreateLineCircleMeasurement(line1, circle, distanceModel);
            return true;
        }

        if (type1 == GeoElementType.Circle && type2 == GeoElementType.Circle &&
            TryParseCircle(element1, out var circle1) &&
            TryParseCircle(element2, out var circle2))
        {
            measurement = CreateCircleCircleMeasurement(circle1, circle2);
            return true;
        }

        error = "Unsupported geometry type combination";
        return false;
    }

    private static GeometryMeasurement CreatePointLineMeasurement(Position point, LineData line, DistanceModel distanceModel)
    {
        var distance = distanceModel == DistanceModel.Segment
            ? MeasurementGeometryHelper.DistancePointToSegment(point.X, point.Y, line)
            : MeasurementGeometryHelper.DistancePointToInfiniteLine(point.X, point.Y, line);
        var footPoint = distanceModel == DistanceModel.Segment
            ? MeasurementGeometryHelper.ProjectPointToSegment(point.X, point.Y, line)
            : MeasurementGeometryHelper.ProjectPointToInfiniteLine(point.X, point.Y, line);

        return new GeometryMeasurement(
            Distance: distance,
            Angle: 0.0,
            Intersection1: footPoint,
            Intersection2: MeasurementGeometryHelper.NoIntersection,
            MeasureType: "Point-Line",
            DistanceMeaning: distanceModel == DistanceModel.Segment ? "PointToSegmentDistance" : "PointToInfiniteLineDistance",
            Relation: "Projected",
            IntersectionCount: 1);
    }

    private static GeometryMeasurement CreateLineCircleMeasurement(LineData line, CircleSpec circle, DistanceModel distanceModel)
    {
        var centerDistance = distanceModel == DistanceModel.Segment
            ? MeasurementGeometryHelper.DistancePointToSegment(circle.CenterX, circle.CenterY, line)
            : MeasurementGeometryHelper.DistancePointToInfiniteLine(circle.CenterX, circle.CenterY, line);
        var boundaryGap = Math.Max(0.0, centerDistance - circle.Radius);
        var intersections = SolveLineCircleIntersections(line, circle, distanceModel == DistanceModel.Segment);
        var relation = centerDistance > circle.Radius
            ? "Separated"
            : Math.Abs(centerDistance - circle.Radius) <= 1e-6
                ? "Tangent"
                : "Overlap";

        return new GeometryMeasurement(
            Distance: boundaryGap,
            Angle: 0.0,
            Intersection1: intersections.Count > 0 ? intersections[0] : MeasurementGeometryHelper.NoIntersection,
            Intersection2: intersections.Count > 1 ? intersections[1] : MeasurementGeometryHelper.NoIntersection,
            MeasureType: "Line-Circle",
            DistanceMeaning: "BoundaryGap",
            Relation: relation,
            IntersectionCount: intersections.Count);
    }

    private static GeometryMeasurement CreateCircleCircleMeasurement(CircleSpec first, CircleSpec second)
    {
        var centerDistance = MeasurementGeometryHelper.Distance(first.CenterX, first.CenterY, second.CenterX, second.CenterY);
        string relation;
        double distance;

        if (centerDistance > first.Radius + second.Radius)
        {
            relation = "Separated";
            distance = centerDistance - first.Radius - second.Radius;
        }
        else if (centerDistance < Math.Abs(first.Radius - second.Radius))
        {
            relation = "Contained";
            distance = Math.Abs(first.Radius - second.Radius) - centerDistance;
        }
        else
        {
            relation = Math.Abs(centerDistance - (first.Radius + second.Radius)) <= 1e-6 ||
                       Math.Abs(centerDistance - Math.Abs(first.Radius - second.Radius)) <= 1e-6
                ? "Tangent"
                : "Intersecting";
            distance = 0.0;
        }

        var intersections = SolveCircleCircleIntersections(first, second);
        return new GeometryMeasurement(
            Distance: distance,
            Angle: 0.0,
            Intersection1: intersections.Count > 0 ? intersections[0] : MeasurementGeometryHelper.NoIntersection,
            Intersection2: intersections.Count > 1 ? intersections[1] : MeasurementGeometryHelper.NoIntersection,
            MeasureType: "Circle-Circle",
            DistanceMeaning: "BoundaryGap",
            Relation: relation,
            IntersectionCount: intersections.Count);
    }

    private static bool TryParseDistanceModel(string model, out DistanceModel parsed)
    {
        parsed = DistanceModel.Segment;
        if (string.Equals(model, "Segment", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.Segment;
            return true;
        }

        if (string.Equals(model, "InfiniteLine", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.InfiniteLine;
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

    private static List<Position> SolveLineCircleIntersections(LineData line, CircleSpec circle, bool clampToSegment)
    {
        var result = new List<Position>();
        var x1 = line.StartX - circle.CenterX;
        var y1 = line.StartY - circle.CenterY;
        var x2 = line.EndX - circle.CenterX;
        var y2 = line.EndY - circle.CenterY;
        var dx = x2 - x1;
        var dy = y2 - y1;

        var a = (dx * dx) + (dy * dy);
        var b = 2 * ((x1 * dx) + (y1 * dy));
        var c = (x1 * x1) + (y1 * y1) - (circle.Radius * circle.Radius);
        var delta = (b * b) - (4 * a * c);
        if (delta < 0 || Math.Abs(a) < 1e-12)
        {
            return result;
        }

        var sqrtDelta = Math.Sqrt(Math.Max(0.0, delta));
        var roots = new[] { (-b + sqrtDelta) / (2 * a), (-b - sqrtDelta) / (2 * a) };
        foreach (var t in roots)
        {
            if (clampToSegment && (t < -1e-9 || t > 1 + 1e-9))
            {
                continue;
            }

            var point = new Position(
                line.StartX + (t * (line.EndX - line.StartX)),
                line.StartY + (t * (line.EndY - line.StartY)));

            if (result.All(existing => MeasurementGeometryHelper.Distance(existing, point) > 1e-6))
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static List<Position> SolveCircleCircleIntersections(CircleSpec first, CircleSpec second)
    {
        var result = new List<Position>();
        var dx = second.CenterX - first.CenterX;
        var dy = second.CenterY - first.CenterY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance < 1e-9 || distance > first.Radius + second.Radius || distance < Math.Abs(first.Radius - second.Radius))
        {
            return result;
        }

        var a = ((first.Radius * first.Radius) - (second.Radius * second.Radius) + (distance * distance)) / (2 * distance);
        var hSquared = (first.Radius * first.Radius) - (a * a);
        if (hSquared < 0)
        {
            return result;
        }

        var h = Math.Sqrt(Math.Max(0.0, hSquared));
        var xm = first.CenterX + ((a * dx) / distance);
        var ym = first.CenterY + ((a * dy) / distance);
        var rx = -dy * (h / distance);
        var ry = dx * (h / distance);

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

        if (obj is Position position)
        {
            point = position;
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
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
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
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0f, StringComparer.OrdinalIgnoreCase);
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
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
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
        Segment = 0,
        InfiniteLine = 1
    }

    private sealed record CircleSpec(double CenterX, double CenterY, double Radius);

    private readonly record struct GeometryMeasurement(
        double Distance,
        double Angle,
        Position Intersection1,
        Position Intersection2,
        string MeasureType,
        string DistanceMeaning,
        string Relation,
        int IntersectionCount);
}
