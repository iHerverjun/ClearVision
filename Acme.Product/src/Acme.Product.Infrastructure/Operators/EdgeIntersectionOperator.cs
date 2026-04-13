// EdgeIntersectionOperator.cs
// 边线交点算子
// 计算两条边线或线段的交点坐标
using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "边线交点",
    Description = "Computes line intersection and angle between two lines.",
    Category = "定位",
    IconName = "intersection",
    Keywords = new[] { "intersection", "cross point", "line angle" }
)]
[InputPort("Line1", "Line 1", PortDataType.LineData, IsRequired = true)]
[InputPort("Line2", "Line 2", PortDataType.LineData, IsRequired = true)]
[OutputPort("Point", "Point", PortDataType.Point)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("HasIntersection", "Has Intersection", PortDataType.Boolean)]
[OutputPort("SegmentsIntersect", "Segments Intersect", PortDataType.Boolean)]
[OperatorParam("IntersectionMode", "Intersection Mode", "enum", DefaultValue = "InfiniteLine", Options = new[] { "InfiniteLine|InfiniteLine", "SegmentOnly|SegmentOnly" })]
public class EdgeIntersectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.EdgeIntersection;

    public EdgeIntersectionOperator(ILogger<EdgeIntersectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Line1 and Line2 are required"));
        }

        if (!inputs.TryGetValue("Line1", out var line1Obj) || !TryParseLine(line1Obj, out var line1))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line1' is missing or invalid"));
        }

        if (!inputs.TryGetValue("Line2", out var line2Obj) || !TryParseLine(line2Obj, out var line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line2' is missing or invalid"));
        }

        if (line1.Length <= 1e-6f || line2.Length <= 1e-6f)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Line1 and Line2 must be non-degenerate line segments"));
        }

        var intersectionMode = GetStringParam(@operator, "IntersectionMode", "InfiniteLine");

        var v1x = line1.EndX - line1.StartX;
        var v1y = line1.EndY - line1.StartY;
        var v2x = line2.EndX - line2.StartX;
        var v2y = line2.EndY - line2.StartY;

        var norm1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var norm2 = Math.Sqrt(v2x * v2x + v2y * v2y);
        var angle = 0.0;
        if (norm1 > 1e-9 && norm2 > 1e-9)
        {
            var cos = Math.Clamp((v1x * v2x + v1y * v2y) / (norm1 * norm2), -1.0, 1.0);
            angle = Math.Acos(cos) * 180.0 / Math.PI;
            if (angle > 90)
            {
                angle = 180 - angle;
            }
        }

        var denominator = (line1.StartX - line1.EndX) * (line2.StartY - line2.EndY) -
                          (line1.StartY - line1.EndY) * (line2.StartX - line2.EndX);
        var hasLineIntersection = Math.Abs(denominator) > 1e-9;

        Position intersection;
        if (hasLineIntersection)
        {
            var pxNumerator = (line1.StartX * line1.EndY - line1.StartY * line1.EndX) * (line2.StartX - line2.EndX) -
                              (line1.StartX - line1.EndX) * (line2.StartX * line2.EndY - line2.StartY * line2.EndX);
            var pyNumerator = (line1.StartX * line1.EndY - line1.StartY * line1.EndX) * (line2.StartY - line2.EndY) -
                              (line1.StartY - line1.EndY) * (line2.StartX * line2.EndY - line2.StartY * line2.EndX);
            intersection = new Position(pxNumerator / denominator, pyNumerator / denominator);
        }
        else
        {
            intersection = new Position(0, 0);
        }

        var segmentsIntersect = hasLineIntersection && DoSegmentsIntersect(line1, line2);
        var hasIntersection = intersectionMode.Equals("SegmentOnly", StringComparison.OrdinalIgnoreCase)
            ? segmentsIntersect
            : hasLineIntersection;

        var output = new Dictionary<string, object>
        {
            { "Point", intersection },
            { "Angle", angle },
            { "HasIntersection", hasIntersection },
            { "SegmentsIntersect", segmentsIntersect }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "IntersectionMode", "InfiniteLine");
        if (!mode.Equals("InfiniteLine", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("SegmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("IntersectionMode must be InfiniteLine or SegmentOnly");
        }

        return ValidationResult.Valid();
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

    private static bool DoSegmentsIntersect(LineData first, LineData second)
    {
        var a = new Position(first.StartX, first.StartY);
        var b = new Position(first.EndX, first.EndY);
        var c = new Position(second.StartX, second.StartY);
        var d = new Position(second.EndX, second.EndY);

        var orientation1 = Orientation(a, b, c);
        var orientation2 = Orientation(a, b, d);
        var orientation3 = Orientation(c, d, a);
        var orientation4 = Orientation(c, d, b);

        if (((orientation1 > 0 && orientation2 < 0) || (orientation1 < 0 && orientation2 > 0)) &&
            ((orientation3 > 0 && orientation4 < 0) || (orientation3 < 0 && orientation4 > 0)))
        {
            return true;
        }

        return Math.Abs(orientation1) <= 1e-9 && OnSegment(a, c, b) ||
               Math.Abs(orientation2) <= 1e-9 && OnSegment(a, d, b) ||
               Math.Abs(orientation3) <= 1e-9 && OnSegment(c, a, d) ||
               Math.Abs(orientation4) <= 1e-9 && OnSegment(c, b, d);
    }

    private static double Orientation(Position a, Position b, Position c)
    {
        return ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
    }

    private static bool OnSegment(Position a, Position p, Position b)
    {
        return p.X >= Math.Min(a.X, b.X) - 1e-9 &&
               p.X <= Math.Max(a.X, b.X) + 1e-9 &&
               p.Y >= Math.Min(a.Y, b.Y) - 1e-9 &&
               p.Y <= Math.Max(a.Y, b.Y) + 1e-9;
    }
}
