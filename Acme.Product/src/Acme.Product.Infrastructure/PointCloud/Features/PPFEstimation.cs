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
    /// Compute a simple PPF "model" map keyed by reference point index.
    /// Each entry stores the PPF features between that reference point and its neighbors within featureRadius.
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

                var ppf = PPFFeature.Compute(p1, n1, p2, n2);
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
            NormalizeNormalsInPlace(normals);
            return normals;
        }

        var estimator = new NormalEstimation(_pool);
        var normalsEstimated = estimator.Estimate(cloud, normalRadius);
        NormalizeNormalsInPlace(normalsEstimated);
        return normalsEstimated;
    }

    private static void NormalizeNormalsInPlace(Mat normals)
    {
        if (normals.Rows == 0)
        {
            return;
        }

        var idx = normals.GetGenericIndexer<float>();
        for (int i = 0; i < normals.Rows; i++)
        {
            var v = new Vector3(idx[i, 0], idx[i, 1], idx[i, 2]);
            if (v.LengthSquared() <= 1e-20f)
            {
                idx[i, 0] = 0;
                idx[i, 1] = 0;
                idx[i, 2] = 1;
                continue;
            }

            v = Vector3.Normalize(v);
            idx[i, 0] = v.X;
            idx[i, 1] = v.Y;
            idx[i, 2] = v.Z;
        }
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
