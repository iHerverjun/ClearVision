using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Features;

public sealed class PPFEstimation
{
    private readonly MatPool _pool;

    public PPFEstimation(MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
    }

    /// <summary>
    /// Compute an alpha-less canonicalized 4D PPF map keyed by reference point index.
    /// Each entry stores features that are stable under a joint normal sign flip, but this is still not a full
    /// canonical PPF vote contract because alpha_m is not included here.
    /// </summary>
    public Dictionary<int, List<PPFFeature>> ComputeModel(
        PointCloud model,
        float normalRadius = 0.03f,
        float featureRadius = 0.05f,
        bool useExistingNormals = true)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (normalRadius <= 0 || !float.IsFinite(normalRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(normalRadius), "normalRadius must be positive and finite.");
        }
        if (featureRadius <= 0 || !float.IsFinite(featureRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(featureRadius), "featureRadius must be positive and finite.");
        }

        int n = model.Count;
        if (n == 0)
        {
            return new Dictionary<int, List<PPFFeature>>(capacity: 0);
        }

        var normals = GetOrEstimateNormals(model, normalRadius, useExistingNormals);
        var nIdx = normals.GetGenericIndexer<float>();
        var points = model.Points.GetGenericIndexer<float>();

        var grid = SpatialHashGrid.Build(points, n, cellSize: featureRadius);
        var r2 = (double)featureRadius * featureRadius;
        var neighbors = new List<int>(capacity: 64);

        var map = new Dictionary<int, List<PPFFeature>>(capacity: n);

        for (int i = 0; i < n; i++)
        {
            neighbors.Clear();
            SpatialHashGrid.CollectRadiusNeighbors(points, i, grid, featureRadius, r2, neighbors);

            var features = new List<PPFFeature>(capacity: neighbors.Count);
            var p1 = new Vector3(points[i, 0], points[i, 1], points[i, 2]);
            var n1 = new Vector3(nIdx[i, 0], nIdx[i, 1], nIdx[i, 2]);

            for (int t = 0; t < neighbors.Count; t++)
            {
                var j = neighbors[t];
                var p2 = new Vector3(points[j, 0], points[j, 1], points[j, 2]);
                var n2 = new Vector3(nIdx[j, 0], nIdx[j, 1], nIdx[j, 2]);

                var ppf = ComputeCanonicalFeature(p1, n1, p2, n2);
                if (ppf.Distance > 0)
                {
                    features.Add(ppf);
                }
            }

            map[i] = features;
        }

        ReturnMat(normals);
        return map;
    }

    /// <summary>
    /// Return a new point cloud with computed normals (always a deep copy to avoid shared disposal).
    /// </summary>
    public PointCloud ComputePointCloudWithNormals(PointCloud input, float normalRadius = 0.03f, bool useExistingNormals = true)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (normalRadius <= 0 || !float.IsFinite(normalRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(normalRadius), "normalRadius must be positive and finite.");
        }

        var n = input.Count;
        var outPoints = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        input.Points.CopyTo(outPoints);

        Mat? outColors = null;
        if (input.Colors != null)
        {
            outColors = _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1);
            input.Colors.CopyTo(outColors);
        }

        var normals = GetOrEstimateNormals(input, normalRadius, useExistingNormals);
        return new PointCloud(outPoints, outColors, normals, isOrganized: false, pool: _pool);
    }

    private Mat GetOrEstimateNormals(PointCloud cloud, float normalRadius, bool useExistingNormals)
    {
        if (useExistingNormals && cloud.Normals != null)
        {
            // Deep copy to keep ownership clear.
            var n = cloud.Count;
            var normals = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
            cloud.Normals.CopyTo(normals);
            NormalEstimation.NormalizeAndOrientConsistently(cloud.Points, normals, normalRadius);
            return normals;
        }

        var estimator = new NormalEstimation(_pool);
        var normalsEstimated = estimator.Estimate(cloud, normalRadius);
        NormalEstimation.NormalizeAndOrientConsistently(cloud.Points, normalsEstimated, normalRadius);
        return normalsEstimated;
    }

    private static PPFFeature ComputeCanonicalFeature(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2)
    {
        var primary = PPFFeature.Compute(p1, n1, p2, n2);
        if (primary.Distance <= 0)
        {
            return primary;
        }

        var flipped = PPFFeature.Compute(p1, -n1, p2, -n2);
        return CompareCanonicalFeature(primary, flipped) <= 0 ? primary : flipped;
    }

    private static int CompareCanonicalFeature(PPFFeature left, PPFFeature right)
    {
        var cmp = left.Distance.CompareTo(right.Distance);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = left.Angle1.CompareTo(right.Angle1);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = left.Angle2.CompareTo(right.Angle2);
        if (cmp != 0)
        {
            return cmp;
        }

        return left.AngleNormals.CompareTo(right.AngleNormals);
    }

    private void ReturnMat(Mat mat)
    {
        if (mat.IsDisposed)
        {
            return;
        }

        if (mat.Rows <= 0 || mat.Cols <= 0)
        {
            mat.Dispose();
            return;
        }

        _pool.Return(mat);
    }
}
