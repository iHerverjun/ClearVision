using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

public static class RoiTracker
{
    /// <summary>
    /// Transforms a base ROI by applying rotation + scale around its center, then translating
    /// so the center lands on <paramref name="matchPosition"/>.
    /// </summary>
    public static Rect TransformRoi(Rect baseRoi, Point2f matchPosition, float matchAngle, float matchScale)
    {
        if (baseRoi.Width <= 0 || baseRoi.Height <= 0)
        {
            return baseRoi;
        }

        var baseCenter = new Point2f(
            baseRoi.X + (baseRoi.Width / 2f),
            baseRoi.Y + (baseRoi.Height / 2f));

        // Rotation matrix keeps baseCenter unchanged, so we add a translation term to move it to matchPosition.
        using var transform = Cv2.GetRotationMatrix2D(baseCenter, matchAngle, matchScale);
        var dx = matchPosition.X - baseCenter.X;
        var dy = matchPosition.Y - baseCenter.Y;
        transform.Set(0, 2, transform.At<double>(0, 2) + dx);
        transform.Set(1, 2, transform.At<double>(1, 2) + dy);

        var corners = new[]
        {
            new Point2f(baseRoi.Left, baseRoi.Top),
            new Point2f(baseRoi.Right, baseRoi.Top),
            new Point2f(baseRoi.Right, baseRoi.Bottom),
            new Point2f(baseRoi.Left, baseRoi.Bottom)
        };

        var transformed = new Point2f[corners.Length];
        for (var i = 0; i < corners.Length; i++)
        {
            transformed[i] = TransformPoint(corners[i], transform);
        }

        return BoundingRect(transformed);
    }

    private static Point2f TransformPoint(Point2f point, Mat transform)
    {
        var m00 = transform.At<double>(0, 0);
        var m01 = transform.At<double>(0, 1);
        var m02 = transform.At<double>(0, 2);
        var m10 = transform.At<double>(1, 0);
        var m11 = transform.At<double>(1, 1);
        var m12 = transform.At<double>(1, 2);

        var x = m00 * point.X + m01 * point.Y + m02;
        var y = m10 * point.X + m11 * point.Y + m12;
        return new Point2f((float)x, (float)y);
    }

    private static Rect BoundingRect(ReadOnlySpan<Point2f> points)
    {
        if (points.IsEmpty)
        {
            return new Rect();
        }

        float minX = points[0].X;
        float maxX = points[0].X;
        float minY = points[0].Y;
        float maxY = points[0].Y;

        for (var i = 1; i < points.Length; i++)
        {
            var p = points[i];
            if (p.X < minX)
            {
                minX = p.X;
            }

            if (p.X > maxX)
            {
                maxX = p.X;
            }

            if (p.Y < minY)
            {
                minY = p.Y;
            }

            if (p.Y > maxY)
            {
                maxY = p.Y;
            }
        }

        var x = (int)Math.Floor(minX);
        var y = (int)Math.Floor(minY);
        var right = (int)Math.Ceiling(maxX);
        var bottom = (int)Math.Ceiling(maxY);

        var w = Math.Max(1, right - x);
        var h = Math.Max(1, bottom - y);
        return new Rect(x, y, w, h);
    }
}

