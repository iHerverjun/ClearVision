using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Segmentation;

public readonly record struct RansacPlaneResult(Vector3 Normal, float D, int[] Inliers)
{
    public int InlierCount => Inliers.Length;
}

/// <summary>
/// RANSAC plane segmentation for point clouds.
/// Returns a plane model (normal, d) for ax+by+cz+d=0 and the inlier indices.
/// </summary>
public sealed class RansacPlaneSegmentation
{
    private readonly Random _rng;
    private readonly MatPool _pool;

    public RansacPlaneSegmentation(int? seed = null, MatPool? pool = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _pool = pool ?? MatPool.Shared;
    }

    public RansacPlaneResult Segment(
        PointCloud cloud,
        float distanceThreshold = 0.01f,
        int maxIterations = 1000,
        int minInliers = 100)
    {
        if (cloud == null) throw new ArgumentNullException(nameof(cloud));
        if (distanceThreshold <= 0 || !float.IsFinite(distanceThreshold))
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), "distanceThreshold must be positive and finite.");
        }
        if (maxIterations <= 0) throw new ArgumentOutOfRangeException(nameof(maxIterations), "maxIterations must be > 0.");
        if (minInliers <= 0) throw new ArgumentOutOfRangeException(nameof(minInliers), "minInliers must be > 0.");

        var n = cloud.Count;
        if (n < 3 || minInliers > n)
        {
            return new RansacPlaneResult(Vector3.Zero, 0, Array.Empty<int>());
        }

        var pIdx = cloud.Points.GetGenericIndexer<float>();

        var scratch = new int[n];
        int bestCount = 0;
        var bestInliers = Array.Empty<int>();
        var bestNormal = Vector3.UnitZ;
        float bestD = 0;

        // Early stop once we have a very dominant plane.
        var earlyStop = Math.Max(minInliers, (int)(n * 0.95));

        for (int iter = 0; iter < maxIterations; iter++)
        {
            if (!TrySample3Distinct(n, out var i1, out var i2, out var i3))
            {
                continue;
            }

            var p1 = new Vector3(pIdx[i1, 0], pIdx[i1, 1], pIdx[i1, 2]);
            var p2 = new Vector3(pIdx[i2, 0], pIdx[i2, 1], pIdx[i2, 2]);
            var p3 = new Vector3(pIdx[i3, 0], pIdx[i3, 1], pIdx[i3, 2]);

            var v1 = p2 - p1;
            var v2 = p3 - p1;
            var normal = Vector3.Cross(v1, v2);
            var len2 = normal.LengthSquared();
            if (len2 < 1e-18f)
            {
                // Degenerate sample.
                continue;
            }

            normal = Vector3.Normalize(normal);
            var d = -Vector3.Dot(normal, p1);

            int count = 0;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(pIdx[i, 0], pIdx[i, 1], pIdx[i, 2]);
                var dist = MathF.Abs(Vector3.Dot(normal, p) + d);
                if (dist <= distanceThreshold)
                {
                    scratch[count++] = i;
                }
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestNormal = normal;
                bestD = d;
                bestInliers = scratch.AsSpan(0, count).ToArray();

                if (bestCount >= earlyStop)
                {
                    break;
                }
            }
        }

        if (bestCount < minInliers)
        {
            return new RansacPlaneResult(Vector3.Zero, 0, Array.Empty<int>());
        }

        // Refine using PCA on inliers (smallest eigenvector of covariance matrix).
        (bestNormal, bestD) = RefinePlanePca(pIdx, bestInliers, bestNormal);

        // Recompute inliers with refined model (keeps behavior consistent with threshold).
        int refinedCount = 0;
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(pIdx[i, 0], pIdx[i, 1], pIdx[i, 2]);
            var dist = MathF.Abs(Vector3.Dot(bestNormal, p) + bestD);
            if (dist <= distanceThreshold)
            {
                scratch[refinedCount++] = i;
            }
        }

        var refinedInliers = scratch.AsSpan(0, refinedCount).ToArray();
        if (refinedInliers.Length < minInliers)
        {
            // Fall back to original inliers if refinement makes it worse.
            refinedInliers = bestInliers;
        }

        return new RansacPlaneResult(bestNormal, bestD, refinedInliers);
    }

    public PointCloud ExtractInlierCloud(PointCloud input, ReadOnlySpan<int> inliers)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var n = inliers.Length;
        var hasColors = input.Colors != null;
        var hasNormals = input.Normals != null;

        if (n == 0)
        {
            return new PointCloud(
                new Mat(0, 3, MatType.CV_32FC1),
                hasColors ? new Mat(0, 3, MatType.CV_8UC1) : null,
                hasNormals ? new Mat(0, 3, MatType.CV_32FC1) : null,
                isOrganized: false,
                pool: _pool);
        }

        var outPoints = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var outColors = hasColors ? _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1) : null;
        var outNormals = hasNormals ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;

        var srcP = input.Points.GetGenericIndexer<float>();
        var dstP = outPoints.GetGenericIndexer<float>();
        var srcC = input.Colors?.GetGenericIndexer<byte>();
        var dstC = outColors?.GetGenericIndexer<byte>();
        var srcN = input.Normals?.GetGenericIndexer<float>();
        var dstN = outNormals?.GetGenericIndexer<float>();

        for (int r = 0; r < n; r++)
        {
            var i = inliers[r];
            dstP[r, 0] = srcP[i, 0];
            dstP[r, 1] = srcP[i, 1];
            dstP[r, 2] = srcP[i, 2];

            if (dstC != null && srcC != null)
            {
                dstC[r, 0] = srcC[i, 0];
                dstC[r, 1] = srcC[i, 1];
                dstC[r, 2] = srcC[i, 2];
            }

            if (dstN != null && srcN != null)
            {
                dstN[r, 0] = srcN[i, 0];
                dstN[r, 1] = srcN[i, 1];
                dstN[r, 2] = srcN[i, 2];
            }
        }

        return new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool);
    }

    private bool TrySample3Distinct(int n, out int i1, out int i2, out int i3)
    {
        // Avoid unbounded loops on tiny n.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            i1 = _rng.Next(n);
            i2 = _rng.Next(n);
            i3 = _rng.Next(n);
            if (i1 != i2 && i1 != i3 && i2 != i3)
            {
                return true;
            }
        }

        i1 = i2 = i3 = 0;
        return false;
    }

    private static (Vector3 Normal, float D) RefinePlanePca(
        OpenCvSharp.MatIndexer<float> points,
        int[] inliers,
        Vector3 fallbackNormal)
    {
        if (inliers.Length < 3)
        {
            return (fallbackNormal, 0);
        }

        // Centroid
        double cx = 0, cy = 0, cz = 0;
        for (int t = 0; t < inliers.Length; t++)
        {
            var i = inliers[t];
            cx += points[i, 0];
            cy += points[i, 1];
            cz += points[i, 2];
        }

        var inv = 1.0 / inliers.Length;
        cx *= inv; cy *= inv; cz *= inv;

        // Covariance
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        for (int t = 0; t < inliers.Length; t++)
        {
            var i = inliers[t];
            var x = points[i, 0] - cx;
            var y = points[i, 1] - cy;
            var z = points[i, 2] - cz;
            xx += x * x;
            xy += x * y;
            xz += x * z;
            yy += y * y;
            yz += y * z;
            zz += z * z;
        }

        xx *= inv; xy *= inv; xz *= inv; yy *= inv; yz *= inv; zz *= inv;

        using var cov = new Mat(3, 3, MatType.CV_64FC1);
        cov.Set(0, 0, xx);
        cov.Set(0, 1, xy);
        cov.Set(0, 2, xz);
        cov.Set(1, 0, xy);
        cov.Set(1, 1, yy);
        cov.Set(1, 2, yz);
        cov.Set(2, 0, xz);
        cov.Set(2, 1, yz);
        cov.Set(2, 2, zz);

        using var eigenValues = new Mat();
        using var eigenVectors = new Mat();
        if (!Cv2.Eigen(cov, eigenValues, eigenVectors))
        {
            return (fallbackNormal, 0);
        }

        // OpenCV returns eigenvalues in descending order and eigenvectors as rows.
        var ev = eigenVectors.GetGenericIndexer<double>();
        var normal = new Vector3((float)ev[2, 0], (float)ev[2, 1], (float)ev[2, 2]);
        if (normal.LengthSquared() < 1e-18f)
        {
            normal = fallbackNormal;
        }
        else
        {
            normal = Vector3.Normalize(normal);
        }

        // Keep normal direction consistent with the original best normal to avoid sign flips.
        if (Vector3.Dot(normal, fallbackNormal) < 0)
        {
            normal = -normal;
        }

        var centroid = new Vector3((float)cx, (float)cy, (float)cz);
        var d = -Vector3.Dot(normal, centroid);
        return (normal, d);
    }
}

