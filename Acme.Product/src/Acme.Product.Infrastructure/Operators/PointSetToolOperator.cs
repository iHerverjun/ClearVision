using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class PointSetToolOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointSetTool;

    public PointSetToolOperator(ILogger<PointSetToolOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetPoints(inputs, "Points1", out var points))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Points1 is required"));
        }

        if (TryGetPoints(inputs, "Points2", out var points2))
        {
            points.AddRange(points2);
        }

        var operation = GetStringParam(@operator, "Operation", "Merge");
        var sortBy = GetStringParam(@operator, "SortBy", "X");
        var minX = GetDoubleParam(@operator, "FilterMinX", double.MinValue);
        var minY = GetDoubleParam(@operator, "FilterMinY", double.MinValue);
        var maxX = GetDoubleParam(@operator, "FilterMaxX", double.MaxValue);
        var maxY = GetDoubleParam(@operator, "FilterMaxY", double.MaxValue);

        List<Position> resultPoints = operation.ToLowerInvariant() switch
        {
            "merge" => points,
            "sort" => SortPoints(points, sortBy),
            "filter" => points.Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY).ToList(),
            "convexhull" => BuildConvexHull(points),
            "boundingrect" => BuildBoundingRectPoints(points),
            _ => points
        };

        var center = resultPoints.Count > 0
            ? new Position(resultPoints.Average(p => p.X), resultPoints.Average(p => p.Y))
            : new Position(0, 0);

        var rect = BuildBoundingRect(resultPoints);
        var output = new Dictionary<string, object>
        {
            { "Points", resultPoints },
            { "Count", resultPoints.Count },
            { "Center", center },
            { "BoundingBox", new Dictionary<string, object> { { "X", rect.X }, { "Y", rect.Y }, { "Width", rect.Width }, { "Height", rect.Height } } }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operation = GetStringParam(@operator, "Operation", "Merge");
        var validOps = new[] { "Merge", "Sort", "Filter", "ConvexHull", "BoundingRect" };
        if (!validOps.Contains(operation, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Operation must be Merge, Sort, Filter, ConvexHull or BoundingRect");
        }

        var sortBy = GetStringParam(@operator, "SortBy", "X");
        var validSort = new[] { "X", "Y", "Distance" };
        if (!validSort.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("SortBy must be X, Y or Distance");
        }

        return ValidationResult.Valid();
    }

    private static List<Position> SortPoints(List<Position> points, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "y" => points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList(),
            "distance" => points.OrderBy(p => p.X * p.X + p.Y * p.Y).ToList(),
            _ => points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList()
        };
    }

    private static List<Position> BuildConvexHull(List<Position> points)
    {
        if (points.Count < 3)
        {
            return points;
        }

        var pts = points.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
        var hull = Cv2.ConvexHull(pts);
        return hull.Select(p => new Position(p.X, p.Y)).ToList();
    }

    private static List<Position> BuildBoundingRectPoints(List<Position> points)
    {
        var rect = BuildBoundingRect(points);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return new List<Position>();
        }

        return new List<Position>
        {
            new(rect.X, rect.Y),
            new(rect.X + rect.Width, rect.Y),
            new(rect.X + rect.Width, rect.Y + rect.Height),
            new(rect.X, rect.Y + rect.Height)
        };
    }

    private static Rect BuildBoundingRect(List<Position> points)
    {
        if (points.Count == 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        var minX = (int)Math.Floor(points.Min(p => p.X));
        var minY = (int)Math.Floor(points.Min(p => p.Y));
        var maxX = (int)Math.Ceiling(points.Max(p => p.X));
        var maxY = (int)Math.Ceiling(points.Max(p => p.Y));
        return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    private static bool TryGetPoints(Dictionary<string, object>? inputs, string key, out List<Position> points)
    {
        points = new List<Position>();
        if (inputs == null || !inputs.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        if (raw is IEnumerable<Position> typed)
        {
            points = typed.ToList();
            return points.Count > 0;
        }

        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParsePoint(item, out var p))
                {
                    points.Add(p);
                }
            }
        }

        return points.Count > 0;
    }

    private static bool TryParsePoint(object? raw, out Position point)
    {
        point = new Position(0, 0);
        if (raw == null)
        {
            return false;
        }

        if (raw is Position p)
        {
            point = p;
            return true;
        }

        if (raw is Point cvPoint)
        {
            point = new Position(cvPoint.X, cvPoint.Y);
            return true;
        }

        if (raw is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (raw is IDictionary legacy)
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

