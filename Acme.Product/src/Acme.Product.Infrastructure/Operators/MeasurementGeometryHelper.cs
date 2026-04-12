using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Operators;

internal static class MeasurementGeometryHelper
{
    public static readonly Position NoIntersection = new(double.NaN, double.NaN);

    public static bool IsFinite(LineData line)
    {
        return double.IsFinite(line.StartX) &&
               double.IsFinite(line.StartY) &&
               double.IsFinite(line.EndX) &&
               double.IsFinite(line.EndY);
    }

    public static double NormalizeLineDirectionDegrees(double angleDegrees)
    {
        var normalized = angleDegrees % 180.0;
        if (normalized >= 90.0)
        {
            normalized -= 180.0;
        }
        else if (normalized < -90.0)
        {
            normalized += 180.0;
        }

        return normalized;
    }

    public static double AngleBetweenLineDirections(LineData first, LineData second)
    {
        var v1x = first.EndX - first.StartX;
        var v1y = first.EndY - first.StartY;
        var v2x = second.EndX - second.StartX;
        var v2y = second.EndY - second.StartY;

        var norm1 = Math.Sqrt((v1x * v1x) + (v1y * v1y));
        var norm2 = Math.Sqrt((v2x * v2x) + (v2y * v2y));
        if (norm1 < 1e-9 || norm2 < 1e-9)
        {
            return 0.0;
        }

        var cosTheta = Math.Clamp(Math.Abs((v1x * v2x) + (v1y * v2y)) / (norm1 * norm2), -1.0, 1.0);
        return Math.Acos(cosTheta) * 180.0 / Math.PI;
    }

    public static double Distance(Position first, Position second)
    {
        return Distance(first.X, first.Y, second.X, second.Y);
    }

    public static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static double DistancePointToInfiniteLine(double px, double py, LineData line)
    {
        var a = line.EndY - line.StartY;
        var b = line.StartX - line.EndX;
        var c = (line.EndX * line.StartY) - (line.StartX * line.EndY);
        var denominator = Math.Sqrt((a * a) + (b * b));
        if (denominator < 1e-9)
        {
            return 0.0;
        }

        return Math.Abs((a * px) + (b * py) + c) / denominator;
    }

    public static Position ProjectPointToInfiniteLine(double px, double py, LineData line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        var norm2 = (dx * dx) + (dy * dy);
        if (norm2 < 1e-9)
        {
            return new Position(line.StartX, line.StartY);
        }

        var t = (((px - line.StartX) * dx) + ((py - line.StartY) * dy)) / norm2;
        return new Position(line.StartX + (t * dx), line.StartY + (t * dy));
    }

    public static Position ProjectPointToSegment(double px, double py, LineData line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        var norm2 = (dx * dx) + (dy * dy);
        if (norm2 < 1e-9)
        {
            return new Position(line.StartX, line.StartY);
        }

        var t = (((px - line.StartX) * dx) + ((py - line.StartY) * dy)) / norm2;
        t = Math.Clamp(t, 0.0, 1.0);
        return new Position(line.StartX + (t * dx), line.StartY + (t * dy));
    }

    public static double DistancePointToSegment(double px, double py, LineData line)
    {
        var foot = ProjectPointToSegment(px, py, line);
        return Distance(px, py, foot.X, foot.Y);
    }

    public static bool TryGetInfiniteLineIntersection(LineData first, LineData second, out Position intersection)
    {
        var denominator = ((first.StartX - first.EndX) * (second.StartY - second.EndY)) -
                          ((first.StartY - first.EndY) * (second.StartX - second.EndX));
        if (Math.Abs(denominator) < 1e-9)
        {
            intersection = NoIntersection;
            return false;
        }

        var determinant1 = (first.StartX * first.EndY) - (first.StartY * first.EndX);
        var determinant2 = (second.StartX * second.EndY) - (second.StartY * second.EndX);

        var px = ((determinant1 * (second.StartX - second.EndX)) - ((first.StartX - first.EndX) * determinant2)) / denominator;
        var py = ((determinant1 * (second.StartY - second.EndY)) - ((first.StartY - first.EndY) * determinant2)) / denominator;
        intersection = new Position(px, py);
        return true;
    }

    public static bool TryGetSegmentIntersection(LineData first, LineData second, out Position intersection)
    {
        if (!TryGetInfiniteLineIntersection(first, second, out var cross))
        {
            intersection = NoIntersection;
            return false;
        }

        if (IsPointOnSegment(cross, first) && IsPointOnSegment(cross, second))
        {
            intersection = cross;
            return true;
        }

        intersection = NoIntersection;
        return false;
    }

    public static bool IsPointOnSegment(Position point, LineData line)
    {
        var minX = Math.Min(line.StartX, line.EndX) - 1e-6;
        var maxX = Math.Max(line.StartX, line.EndX) + 1e-6;
        var minY = Math.Min(line.StartY, line.EndY) - 1e-6;
        var maxY = Math.Max(line.StartY, line.EndY) + 1e-6;
        return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
    }

    public static double DistanceSegmentToSegment(LineData first, LineData second)
    {
        if (TryGetSegmentIntersection(first, second, out _))
        {
            return 0.0;
        }

        var d1 = DistancePointToSegment(first.StartX, first.StartY, second);
        var d2 = DistancePointToSegment(first.EndX, first.EndY, second);
        var d3 = DistancePointToSegment(second.StartX, second.StartY, first);
        var d4 = DistancePointToSegment(second.EndX, second.EndY, first);
        return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
    }
}
