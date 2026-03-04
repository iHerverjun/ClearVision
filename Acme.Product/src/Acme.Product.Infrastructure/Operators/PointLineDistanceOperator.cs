// PointLineDistanceOperator.cs
// 点线距离算子
// 计算点到线段或直线的距离与投影
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
/// Computes perpendicular distance from a point to a line segment's infinite line.
/// </summary>
[OperatorMeta(
    DisplayName = "点线距离",
    Description = "Computes perpendicular distance from a point to a line.",
    Category = "检测",
    IconName = "distance",
    Keywords = new[] { "point", "line", "distance", "perpendicular" }
)]
[InputPort("Point", "Point", PortDataType.Point, IsRequired = true)]
[InputPort("Line", "Line", PortDataType.LineData, IsRequired = true)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("FootPoint", "Foot Point", PortDataType.Point)]
public class PointLineDistanceOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointLineDistance;

    public PointLineDistanceOperator(ILogger<PointLineDistanceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PointLineDistance requires Point and Line inputs"));
        }

        if (!inputs.TryGetValue("Point", out var pointObj) || !TryParsePoint(pointObj, out var point))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Point' is missing or invalid"));
        }

        if (!inputs.TryGetValue("Line", out var lineObj) || !TryParseLine(lineObj, out var line))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line' is missing or invalid"));
        }

        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        var norm2 = dx * dx + dy * dy;
        if (norm2 < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Line is degenerate (zero length)"));
        }

        var t = ((point.X - line.StartX) * dx + (point.Y - line.StartY) * dy) / norm2;
        var footX = line.StartX + t * dx;
        var footY = line.StartY + t * dy;

        var distX = point.X - footX;
        var distY = point.Y - footY;
        var distance = Math.Sqrt(distX * distX + distY * distY);

        var output = new Dictionary<string, object>
        {
            { "Distance", distance },
            { "FootPoint", new Position(footX, footY) },
            { "FootPointX", footX },
            { "FootPointY", footY }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
            return false;

        switch (obj)
        {
            case Position p:
                point = p;
                return true;
            case Point cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2f cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2d cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            if (TryGetDouble(normalized, "X", out var parsedX) && TryGetDouble(normalized, "Y", out var parsedY))
            {
                point = new Position(parsedX, parsedY);
                return true;
            }
        }

        var text = obj.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var stripped = text.Trim().Trim('(', ')', '[', ']');
            var parts = stripped.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var px) &&
                double.TryParse(parts[1], out var py))
            {
                point = new Position(px, py);
                return true;
            }
        }

        return false;
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

        if (raw is double d)
        {
            value = d;
            return true;
        }

        if (raw is float f)
        {
            value = f;
            return true;
        }

        if (raw is int i)
        {
            value = i;
            return true;
        }

        return double.TryParse(raw.ToString(), out value);
    }
}


