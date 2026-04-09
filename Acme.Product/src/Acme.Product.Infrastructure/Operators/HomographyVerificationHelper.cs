using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal static class HomographyVerificationHelper
{
    internal readonly record struct HomographyVerificationMetrics(
        bool VerificationPassed,
        int MatchCount,
        int InlierCount,
        double InlierRatio,
        double MeanReprojectionError,
        double MaxReprojectionError,
        double AreaRatio,
        bool CornersValid,
        string FailureReason);

    public static bool TryEstimateAndVerify(
        Point2f[] templatePoints,
        Point2f[] searchPoints,
        Size templateSize,
        Size searchImageSize,
        double ransacThreshold,
        int minMatchCount,
        int minInliers,
        double minInlierRatio,
        out Mat? homography,
        out Point2f[] corners,
        out HomographyVerificationMetrics metrics)
    {
        homography = null;
        corners = Array.Empty<Point2f>();
        metrics = new HomographyVerificationMetrics(
            VerificationPassed: false,
            MatchCount: templatePoints.Length,
            InlierCount: 0,
            InlierRatio: 0,
            MeanReprojectionError: double.PositiveInfinity,
            MaxReprojectionError: double.PositiveInfinity,
            AreaRatio: 0,
            CornersValid: false,
            FailureReason: "Homography estimation failed.");

        if (templatePoints.Length != searchPoints.Length || templatePoints.Length < 4)
        {
            metrics = metrics with { FailureReason = "At least four point correspondences are required." };
            return false;
        }

        using var mask = new Mat();
        using var estimated = Cv2.FindHomography(
            InputArray.Create(templatePoints),
            InputArray.Create(searchPoints),
            HomographyMethods.Ransac,
            ransacThreshold,
            mask);

        if (estimated.Empty())
        {
            return false;
        }

        var inlierFlags = ReadInlierFlags(mask, templatePoints.Length);
        var inlierCount = inlierFlags.Count(flag => flag);
        var inlierRatio = (double)inlierCount / templatePoints.Length;

        var projected = Cv2.PerspectiveTransform(templatePoints, estimated);
        var (meanReprojectionError, maxReprojectionError) = ComputeReprojectionErrors(projected, searchPoints, inlierFlags);

        corners = Cv2.PerspectiveTransform(
            new[]
            {
                new Point2f(0, 0),
                new Point2f(templateSize.Width, 0),
                new Point2f(templateSize.Width, templateSize.Height),
                new Point2f(0, templateSize.Height)
            },
            estimated);

        var areaRatio = ComputeAreaRatio(corners, templateSize);
        var cornersValid = AreCornersValid(corners, areaRatio, searchImageSize);
        var maxMeanReprojectionError = Math.Max(1.0, ransacThreshold * 1.35);
        var maxPeakReprojectionError = Math.Max(2.0, ransacThreshold * 2.5);

        var failureReason = string.Empty;
        if (templatePoints.Length < minMatchCount)
        {
            failureReason = $"Insufficient feature matches ({templatePoints.Length} < {minMatchCount}).";
        }
        else if (inlierCount < minInliers)
        {
            failureReason = $"Insufficient inliers ({inlierCount} < {minInliers}).";
        }
        else if (inlierRatio < minInlierRatio)
        {
            failureReason = $"Inlier ratio too low ({inlierRatio:F2} < {minInlierRatio:F2}).";
        }
        else if (meanReprojectionError > maxMeanReprojectionError || maxReprojectionError > maxPeakReprojectionError)
        {
            failureReason = $"Reprojection error too large (mean={meanReprojectionError:F2}px, max={maxReprojectionError:F2}px).";
        }
        else if (!cornersValid)
        {
            failureReason = "Projected quadrilateral is invalid.";
        }

        var verificationPassed = string.IsNullOrEmpty(failureReason);
        metrics = new HomographyVerificationMetrics(
            VerificationPassed: verificationPassed,
            MatchCount: templatePoints.Length,
            InlierCount: inlierCount,
            InlierRatio: inlierRatio,
            MeanReprojectionError: meanReprojectionError,
            MaxReprojectionError: maxReprojectionError,
            AreaRatio: areaRatio,
            CornersValid: cornersValid,
            FailureReason: verificationPassed ? string.Empty : failureReason);

        homography = estimated.Clone();
        return verificationPassed;
    }

    public static double ComputeVerificationScore(
        HomographyVerificationMetrics metrics,
        double ransacThreshold)
    {
        if (!metrics.CornersValid || metrics.InlierCount <= 0 || !double.IsFinite(metrics.MeanReprojectionError))
        {
            return 0;
        }

        var inlierScore = Math.Clamp(metrics.InlierRatio, 0, 1);
        var reprojectionPenalty = Math.Clamp(metrics.MeanReprojectionError / Math.Max(1.0, ransacThreshold * 1.35), 0, 1);
        var reprojectionScore = 1.0 - reprojectionPenalty;
        var areaScore = metrics.AreaRatio switch
        {
            < 0.25 => metrics.AreaRatio / 0.25,
            > 4.0 => Math.Max(0, 1.0 - ((metrics.AreaRatio - 4.0) / 4.0)),
            _ => 1.0
        };

        var score = (inlierScore * 0.55) + (reprojectionScore * 0.35) + (areaScore * 0.10);
        return Math.Clamp(score, 0, 1);
    }

    private static bool[] ReadInlierFlags(Mat mask, int expectedCount)
    {
        var flags = new bool[expectedCount];
        if (mask.Empty())
        {
            return flags;
        }

        if (mask.Rows == expectedCount)
        {
            for (var index = 0; index < expectedCount; index++)
            {
                flags[index] = mask.Get<byte>(index, 0) != 0;
            }

            return flags;
        }

        if (mask.Cols == expectedCount)
        {
            for (var index = 0; index < expectedCount; index++)
            {
                flags[index] = mask.Get<byte>(0, index) != 0;
            }
        }

        return flags;
    }

    private static (double Mean, double Max) ComputeReprojectionErrors(
        Point2f[] projected,
        Point2f[] expected,
        IReadOnlyList<bool> inlierFlags)
    {
        double sum = 0;
        double max = 0;
        var count = 0;

        for (var index = 0; index < projected.Length; index++)
        {
            if (!inlierFlags[index])
            {
                continue;
            }

            var dx = projected[index].X - expected[index].X;
            var dy = projected[index].Y - expected[index].Y;
            var error = Math.Sqrt((dx * dx) + (dy * dy));
            sum += error;
            max = Math.Max(max, error);
            count++;
        }

        if (count == 0)
        {
            return (double.PositiveInfinity, double.PositiveInfinity);
        }

        return (sum / count, max);
    }

    private static double ComputeAreaRatio(Point2f[] corners, Size templateSize)
    {
        if (corners.Length != 4 || templateSize.Width <= 0 || templateSize.Height <= 0)
        {
            return 0;
        }

        var area = Math.Abs(SignedArea(corners));
        var templateArea = templateSize.Width * templateSize.Height;
        return templateArea <= 0 ? 0 : area / templateArea;
    }

    private static bool AreCornersValid(Point2f[] corners, double areaRatio, Size searchImageSize)
    {
        if (corners.Length != 4)
        {
            return false;
        }

        if (corners.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
        {
            return false;
        }

        if (Math.Abs(SignedArea(corners)) < 1e-3)
        {
            return false;
        }

        if (areaRatio is < 0.1 or > 8.0)
        {
            return false;
        }

        if (!IsConvexQuadrilateral(corners) || HasSelfIntersection(corners))
        {
            return false;
        }

        var insideCount = corners.Count(point =>
            point.X >= -1 &&
            point.Y >= -1 &&
            point.X <= searchImageSize.Width + 1 &&
            point.Y <= searchImageSize.Height + 1);

        return insideCount >= 3;
    }

    private static double SignedArea(Point2f[] polygon)
    {
        double area = 0;
        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5;
    }

    private static bool IsConvexQuadrilateral(Point2f[] corners)
    {
        double? sign = null;
        for (var index = 0; index < corners.Length; index++)
        {
            var a = corners[index];
            var b = corners[(index + 1) % corners.Length];
            var c = corners[(index + 2) % corners.Length];
            var cross = Cross(b - a, c - b);
            if (Math.Abs(cross) < 1e-6)
            {
                return false;
            }

            if (sign == null)
            {
                sign = Math.Sign(cross);
                continue;
            }

            if (Math.Sign(cross) != sign.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSelfIntersection(Point2f[] corners)
    {
        return SegmentsIntersect(corners[0], corners[1], corners[2], corners[3]) ||
               SegmentsIntersect(corners[1], corners[2], corners[3], corners[0]);
    }

    private static bool SegmentsIntersect(Point2f a1, Point2f a2, Point2f b1, Point2f b2)
    {
        var d1 = Cross(a2 - a1, b1 - a1);
        var d2 = Cross(a2 - a1, b2 - a1);
        var d3 = Cross(b2 - b1, a1 - b1);
        var d4 = Cross(b2 - b1, a2 - b1);
        return (d1 * d2) < 0 && (d3 * d4) < 0;
    }

    private static double Cross(Point2f a, Point2f b) => (a.X * b.Y) - (a.Y * b.X);
}
