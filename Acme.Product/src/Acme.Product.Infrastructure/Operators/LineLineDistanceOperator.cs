// LineLineDistanceOperator.cs
// 线线距离算子
// 计算两条线段或直线之间的最短距离
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Computes distance/angle/intersection between two lines.
/// </summary>
[OperatorMeta(
    DisplayName = "线线距离",
    Description = "Computes distance and angle between two lines.",
    Category = "检测",
    IconName = "parallel",
    Keywords = new[] { "line distance", "angle", "parallel" }
)]
[InputPort("Line1", "Line 1", PortDataType.LineData, IsRequired = true)]
[InputPort("Line2", "Line 2", PortDataType.LineData, IsRequired = true)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("Intersection", "Intersection", PortDataType.Point)]
[OutputPort("HasIntersection", "Has Intersection", PortDataType.Boolean)]
[OutputPort("IsParallel", "Is Parallel", PortDataType.Boolean)]
[OperatorParam("ParallelThreshold", "Parallel Threshold", "double", DefaultValue = 2.0, Min = 0.0, Max = 45.0)]
public class LineLineDistanceOperator : OperatorBase
{
    private static readonly Position NoIntersection = new(double.NaN, double.NaN);

    public override OperatorType OperatorType => OperatorType.LineLineDistance;

    public LineLineDistanceOperator(ILogger<LineLineDistanceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("LineLineDistance requires Line1 and Line2"));
        }

        if (!inputs.TryGetValue("Line1", out var line1Obj) || !TryParseLine(line1Obj, out var line1))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line1' is missing or invalid"));
        }

        if (!inputs.TryGetValue("Line2", out var line2Obj) || !TryParseLine(line2Obj, out var line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line2' is missing or invalid"));
        }

        var parallelThreshold = GetDoubleParam(@operator, "ParallelThreshold", 2.0, 0, 45);

        var v1x = line1.EndX - line1.StartX;
        var v1y = line1.EndY - line1.StartY;
        var v2x = line2.EndX - line2.StartX;
        var v2y = line2.EndY - line2.StartY;

        var len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var len2 = Math.Sqrt(v2x * v2x + v2y * v2y);

        if (!IsFiniteLine(line1) || !IsFiniteLine(line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Line coordinates must be finite numbers"));
        }

        if (len1 < 1e-9 || len2 < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Input line is zero length"));
        }

        var dot = v1x * v2x + v1y * v2y;
        var cosTheta = Math.Clamp(Math.Abs(dot) / (len1 * len2), -1.0, 1.0);
        var angleDeg = Math.Acos(cosTheta) * 180.0 / Math.PI;

        var isParallel = angleDeg <= parallelThreshold;
        var hasGeometricIntersection = TrySolveIntersection(line1, line2, out var geometricIntersection);
        var hasIntersection = !isParallel && hasGeometricIntersection;
        var intersection = hasIntersection ? geometricIntersection : NoIntersection;

        double distance;
        if (isParallel)
        {
            var midX = (line1.StartX + line1.EndX) / 2.0;
            var midY = (line1.StartY + line1.EndY) / 2.0;
            distance = DistancePointToLine(midX, midY, line2);
        }
        else
        {
            distance = 0;
        }

        var output = new Dictionary<string, object>
        {
            { "Distance", distance },
            { "Angle", angleDeg },
            { "Intersection", intersection },
            { "HasIntersection", hasIntersection },
            { "IsParallel", isParallel },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", isParallel ? 0.01 : 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "ParallelThreshold", 2.0);
        if (threshold < 0 || threshold > 45)
        {
            return ValidationResult.Invalid("ParallelThreshold must be within [0, 45]");
        }

        return ValidationResult.Valid();
    }

    private static bool TrySolveIntersection(LineData l1, LineData l2, out Position intersection)
    {
        var x1 = l1.StartX;
        var y1 = l1.StartY;
        var x2 = l1.EndX;
        var y2 = l1.EndY;

        var x3 = l2.StartX;
        var y3 = l2.StartY;
        var x4 = l2.EndX;
        var y4 = l2.EndY;

        var denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-9)
        {
            intersection = NoIntersection;
            return false;
        }

        var det1 = x1 * y2 - y1 * x2;
        var det2 = x3 * y4 - y3 * x4;

        var px = (det1 * (x3 - x4) - (x1 - x2) * det2) / denom;
        var py = (det1 * (y3 - y4) - (y1 - y2) * det2) / denom;
        intersection = new Position(px, py);
        return true;
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
            if (TryGetDouble(dict, "StartX", out var sx) &&
                TryGetDouble(dict, "StartY", out var sy) &&
                TryGetDouble(dict, "EndX", out var ex) &&
                TryGetDouble(dict, "EndY", out var ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }

            if (TryGetDouble(dict, "X1", out sx) &&
                TryGetDouble(dict, "Y1", out sy) &&
                TryGetDouble(dict, "X2", out ex) &&
                TryGetDouble(dict, "Y2", out ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParseLine(normalized, out line);
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

    private static bool IsFiniteLine(LineData line)
    {
        return double.IsFinite(line.StartX) &&
               double.IsFinite(line.StartY) &&
               double.IsFinite(line.EndX) &&
               double.IsFinite(line.EndY);
    }
}


