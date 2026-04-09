using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal static class LocalDeformableMovingLeastSquaresHelper
{
    private const double Epsilon = 1e-4;

    internal sealed class DeformationModel
    {
        private readonly Point2f[] _sourcePoints;
        private readonly Point2f[] _destinationPoints;
        private readonly double _lambda;

        internal DeformationModel(Point2f[] sourcePoints, Point2f[] destinationPoints, double lambda)
        {
            _sourcePoints = sourcePoints;
            _destinationPoints = destinationPoints;
            _lambda = Math.Max(1e-4, lambda);
        }

        internal int ControlPointCount => _sourcePoints.Length;

        internal Point2f[] SourcePoints => _sourcePoints.ToArray();

        internal Point2f[] DestinationPoints => _destinationPoints.ToArray();

        internal bool TryMapForward(Point2f sourcePoint, out Point2f destinationPoint)
        {
            return TryMapPoint(sourcePoint, _sourcePoints, _destinationPoints, _lambda, out destinationPoint);
        }

        internal bool TryMapInverse(Point2f destinationPoint, out Point2f sourcePoint)
        {
            return TryMapPoint(destinationPoint, _destinationPoints, _sourcePoints, _lambda, out sourcePoint);
        }

        internal Point2f[] MapForward(Point2f[] sourcePoints)
        {
            var mapped = new Point2f[sourcePoints.Length];
            for (var index = 0; index < sourcePoints.Length; index++)
            {
                mapped[index] = TryMapForward(sourcePoints[index], out var destinationPoint)
                    ? destinationPoint
                    : sourcePoints[index];
            }

            return mapped;
        }

        internal (Mat WarpedImage, Mat Mask) Warp(Mat templateImage, Size outputSize)
        {
            var mapX = new Mat(outputSize, MatType.CV_32FC1, Scalar.All(-1));
            var mapY = new Mat(outputSize, MatType.CV_32FC1, Scalar.All(-1));
            var mask = new Mat(outputSize, MatType.CV_8UC1, Scalar.All(0));

            for (var y = 0; y < outputSize.Height; y++)
            {
                for (var x = 0; x < outputSize.Width; x++)
                {
                    if (!TryMapInverse(new Point2f(x, y), out var sourcePoint))
                    {
                        continue;
                    }

                    if (sourcePoint.X < 0 || sourcePoint.X >= templateImage.Width || sourcePoint.Y < 0 || sourcePoint.Y >= templateImage.Height)
                    {
                        continue;
                    }

                    mapX.Set(y, x, sourcePoint.X);
                    mapY.Set(y, x, sourcePoint.Y);
                    mask.Set(y, x, (byte)255);
                }
            }

            var warpedImage = new Mat();
            Cv2.Remap(templateImage, warpedImage, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

            mapX.Dispose();
            mapY.Dispose();
            return (warpedImage, mask);
        }
    }

    internal static bool TryCreate(
        IReadOnlyList<Point2f> sourcePoints,
        IReadOnlyList<Point2f> destinationPoints,
        double lambda,
        out DeformationModel? model,
        out string failureReason)
    {
        model = null;
        failureReason = string.Empty;

        if (sourcePoints.Count != destinationPoints.Count || sourcePoints.Count < 3)
        {
            failureReason = "At least three verified control points are required for local deformation.";
            return false;
        }

        if (ArePointsDegenerate(sourcePoints))
        {
            failureReason = "Verified control points are degenerate.";
            return false;
        }

        model = new DeformationModel(sourcePoints.ToArray(), destinationPoints.ToArray(), lambda);
        return true;
    }

    private static bool TryMapPoint(
        Point2f queryPoint,
        IReadOnlyList<Point2f> sourcePoints,
        IReadOnlyList<Point2f> destinationPoints,
        double lambda,
        out Point2f mappedPoint)
    {
        for (var index = 0; index < sourcePoints.Count; index++)
        {
            if (DistanceSquared(queryPoint, sourcePoints[index]) <= Epsilon)
            {
                mappedPoint = destinationPoints[index];
                return true;
            }
        }

        var weightSum = 0.0;
        var sourceCenterX = 0.0;
        var sourceCenterY = 0.0;
        var destinationCenterX = 0.0;
        var destinationCenterY = 0.0;

        var weights = new double[sourcePoints.Count];
        for (var index = 0; index < sourcePoints.Count; index++)
        {
            var weight = 1.0 / (DistanceSquared(queryPoint, sourcePoints[index]) + lambda);
            weights[index] = weight;
            weightSum += weight;
            sourceCenterX += weight * sourcePoints[index].X;
            sourceCenterY += weight * sourcePoints[index].Y;
            destinationCenterX += weight * destinationPoints[index].X;
            destinationCenterY += weight * destinationPoints[index].Y;
        }

        if (weightSum <= Epsilon)
        {
            mappedPoint = default;
            return false;
        }

        sourceCenterX /= weightSum;
        sourceCenterY /= weightSum;
        destinationCenterX /= weightSum;
        destinationCenterY /= weightSum;

        var m00 = lambda;
        var m01 = 0.0;
        var m11 = lambda;
        var b00 = 0.0;
        var b01 = 0.0;
        var b10 = 0.0;
        var b11 = 0.0;

        for (var index = 0; index < sourcePoints.Count; index++)
        {
            var weight = weights[index];
            var sourceDx = sourcePoints[index].X - sourceCenterX;
            var sourceDy = sourcePoints[index].Y - sourceCenterY;
            var destinationDx = destinationPoints[index].X - destinationCenterX;
            var destinationDy = destinationPoints[index].Y - destinationCenterY;

            m00 += weight * sourceDx * sourceDx;
            m01 += weight * sourceDx * sourceDy;
            m11 += weight * sourceDy * sourceDy;

            b00 += weight * destinationDx * sourceDx;
            b01 += weight * destinationDx * sourceDy;
            b10 += weight * destinationDy * sourceDx;
            b11 += weight * destinationDy * sourceDy;
        }

        var determinant = (m00 * m11) - (m01 * m01);
        if (Math.Abs(determinant) <= Epsilon)
        {
            mappedPoint = default;
            return false;
        }

        var inverseDeterminant = 1.0 / determinant;
        var inverse00 = m11 * inverseDeterminant;
        var inverse01 = -m01 * inverseDeterminant;
        var inverse10 = -m01 * inverseDeterminant;
        var inverse11 = m00 * inverseDeterminant;

        var a00 = (b00 * inverse00) + (b01 * inverse10);
        var a01 = (b00 * inverse01) + (b01 * inverse11);
        var a10 = (b10 * inverse00) + (b11 * inverse10);
        var a11 = (b10 * inverse01) + (b11 * inverse11);

        var queryDx = queryPoint.X - sourceCenterX;
        var queryDy = queryPoint.Y - sourceCenterY;

        mappedPoint = new Point2f(
            (float)((a00 * queryDx) + (a01 * queryDy) + destinationCenterX),
            (float)((a10 * queryDx) + (a11 * queryDy) + destinationCenterY));
        return float.IsFinite(mappedPoint.X) && float.IsFinite(mappedPoint.Y);
    }

    private static bool ArePointsDegenerate(IReadOnlyList<Point2f> points)
    {
        if (points.Count < 3)
        {
            return true;
        }

        for (var i = 0; i < points.Count - 2; i++)
        {
            for (var j = i + 1; j < points.Count - 1; j++)
            {
                for (var k = j + 1; k < points.Count; k++)
                {
                    var area = Math.Abs(
                        (points[j].X - points[i].X) * (points[k].Y - points[i].Y) -
                        (points[j].Y - points[i].Y) * (points[k].X - points[i].X));
                    if (area > 1e-2)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static double DistanceSquared(Point2f a, Point2f b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
