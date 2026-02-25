using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Computes distance/angle/intersection between two lines.
/// </summary>
public class LineLineDistanceOperator : OperatorBase
{
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

        if (len1 < 1e-9 || len2 < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input line is degenerate"));
        }

        var dot = v1x * v2x + v1y * v2y;
        var cosTheta = Math.Clamp(Math.Abs(dot) / (len1 * len2), -1.0, 1.0);
        var angleDeg = Math.Acos(cosTheta) * 180.0 / Math.PI;

        var isParallel = angleDeg <= parallelThreshold;

        var intersection = SolveIntersection(line1, line2, out var hasIntersection);

        double distance;
        if (isParallel)
        {
            var midX = (line1.StartX + line1.EndX) / 2.0;
            var midY = (line1.StartY + line1.EndY) / 2.0;
            distance = DistancePointToLine(midX, midY, line2);

            if (!hasIntersection)
            {
                intersection = new Position((line1.MidX + line2.MidX) / 2.0, (line1.MidY + line2.MidY) / 2.0);
            }
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
            { "IsParallel", isParallel }
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

    private static Position SolveIntersection(LineData l1, LineData l2, out bool hasIntersection)
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
            hasIntersection = false;
            return new Position((l1.MidX + l2.MidX) / 2.0, (l1.MidY + l2.MidY) / 2.0);
        }

        var det1 = x1 * y2 - y1 * x2;
        var det2 = x3 * y4 - y3 * x4;

        var px = (det1 * (x3 - x4) - (x1 - x2) * det2) / denom;
        var py = (det1 * (y3 - y4) - (y1 - y2) * det2) / denom;
        hasIntersection = true;
        return new Position(px, py);
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
}

