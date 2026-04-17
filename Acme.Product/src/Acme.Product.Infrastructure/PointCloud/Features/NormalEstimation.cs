using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Features;

public sealed class NormalEstimation
{
    private readonly MatPool _pool;

    public NormalEstimation(MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
    }

    /// <summary>
    /// Estimate normals using PCA (smallest eigenvector of covariance matrix) within a radius.
    /// Returns a new Nx3 CV_32FC1 Mat with normalized, locally consistent orientation.
    /// </summary>
    public Mat Estimate(PointCloud cloud, float radius)
    {
        if (cloud == null) throw new ArgumentNullException(nameof(cloud));
        if (radius <= 0 || !float.IsFinite(radius))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "radius must be positive and finite.");
        }

        int n = cloud.Count;
        if (n == 0)
        {
            return new Mat(0, 3, MatType.CV_32FC1);
        }

        var points = cloud.Points.GetGenericIndexer<float>();
        var normals = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var outN = normals.GetGenericIndexer<float>();

        // Spatial hash grid for radius queries.
        var grid = SpatialHashGrid.Build(points, n, cellSize: radius);
        var r2 = (double)radius * radius;

        var neighbors = new List<int>(capacity: 64);
        for (int i = 0; i < n; i++)
        {
            neighbors.Clear();

            SpatialHashGrid.CollectRadiusNeighbors(points, i, grid, radius, r2, neighbors);

            // Include self for stability if missing.
            if (neighbors.Count < 3)
            {
                outN[i, 0] = 0;
                outN[i, 1] = 0;
                outN[i, 2] = 1;
                continue;
            }

            // Centroid
            double cx = 0, cy = 0, cz = 0;
            for (int t = 0; t < neighbors.Count; t++)
            {
                var idx = neighbors[t];
                cx += points[idx, 0];
                cy += points[idx, 1];
                cz += points[idx, 2];
            }

            var inv = 1.0 / neighbors.Count;
            cx *= inv; cy *= inv; cz *= inv;

            // Covariance
            double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int t = 0; t < neighbors.Count; t++)
            {
                var idx = neighbors[t];
                var x = points[idx, 0] - cx;
                var y = points[idx, 1] - cy;
                var z = points[idx, 2] - cz;
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
                outN[i, 0] = 0;
                outN[i, 1] = 0;
                outN[i, 2] = 1;
                continue;
            }

            // Smallest eigenvector as normal (OpenCV rows are eigenvectors in descending eigenvalue order).
            var ev = eigenVectors.GetGenericIndexer<double>();
            var normal = new Vector3((float)ev[2, 0], (float)ev[2, 1], (float)ev[2, 2]);
            if (normal.LengthSquared() <= 1e-20f)
            {
                normal = Vector3.UnitZ;
            }
            else
            {
                normal = Vector3.Normalize(normal);
            }

            outN[i, 0] = normal.X;
            outN[i, 1] = normal.Y;
            outN[i, 2] = normal.Z;
        }

        NormalizeAndOrientConsistently(cloud.Points, normals, radius);
        return normals;
    }

    public static void NormalizeAndOrientConsistently(Mat points, Mat normals, float neighborRadius)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        if (normals == null) throw new ArgumentNullException(nameof(normals));
        if (neighborRadius <= 0 || !float.IsFinite(neighborRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(neighborRadius), "neighborRadius must be positive and finite.");
        }
        if (points.Rows != normals.Rows || points.Cols != 3 || normals.Cols != 3)
        {
            throw new ArgumentException("points and normals must both be Nx3 with matching row counts.");
        }
        if (points.Rows == 0)
        {
            return;
        }

        NormalizeNormalsInPlace(normals);

        var pointIdx = points.GetGenericIndexer<float>();
        var normalIdx = normals.GetGenericIndexer<float>();
        var grid = SpatialHashGrid.Build(pointIdx, points.Rows, cellSize: neighborRadius);
        var r2 = (double)neighborRadius * neighborRadius;
        var centroid = ComputeCentroid(pointIdx, points.Rows);
        var visited = new bool[points.Rows];
        var neighbors = new List<int>(capacity: 64);
        var queue = new Queue<int>(capacity: 64);
        var component = new List<int>(capacity: 128);

        for (int seed = 0; seed < points.Rows; seed++)
        {
            if (visited[seed])
            {
                continue;
            }

            component.Clear();
            visited[seed] = true;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                neighbors.Clear();
                SpatialHashGrid.CollectRadiusNeighbors(pointIdx, current, grid, neighborRadius, r2, neighbors);
                var currentNormal = ReadVector(normalIdx, current);

                for (int t = 0; t < neighbors.Count; t++)
                {
                    var neighbor = neighbors[t];
                    if (neighbor == current || visited[neighbor])
                    {
                        continue;
                    }

                    var neighborNormal = ReadVector(normalIdx, neighbor);
                    if (Vector3.Dot(currentNormal, neighborNormal) < 0f)
                    {
                        WriteVector(normalIdx, neighbor, -neighborNormal);
                    }

                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            AlignComponentWithGeometry(pointIdx, normalIdx, component, centroid);
        }
    }

    private static void NormalizeNormalsInPlace(Mat normals)
    {
        var idx = normals.GetGenericIndexer<float>();
        for (int i = 0; i < normals.Rows; i++)
        {
            var v = ReadVector(idx, i);
            if (v.LengthSquared() <= 1e-20f || !float.IsFinite(v.LengthSquared()))
            {
                WriteVector(idx, i, Vector3.UnitZ);
                continue;
            }

            WriteVector(idx, i, Vector3.Normalize(v));
        }
    }

    private static void AlignComponentWithGeometry(
        MatIndexer<float> points,
        MatIndexer<float> normals,
        List<int> component,
        Vector3 centroid)
    {
        if (component.Count == 0)
        {
            return;
        }

        double radialScore = 0;
        for (int i = 0; i < component.Count; i++)
        {
            var index = component[i];
            radialScore += Vector3.Dot(ReadVector(normals, index), ReadVector(points, index) - centroid);
        }

        if (radialScore >= 0)
        {
            return;
        }

        for (int i = 0; i < component.Count; i++)
        {
            var index = component[i];
            WriteVector(normals, index, -ReadVector(normals, index));
        }
    }

    private static Vector3 ComputeCentroid(MatIndexer<float> points, int count)
    {
        double cx = 0;
        double cy = 0;
        double cz = 0;
        for (int i = 0; i < count; i++)
        {
            cx += points[i, 0];
            cy += points[i, 1];
            cz += points[i, 2];
        }

        var inv = 1.0 / count;
        return new Vector3((float)(cx * inv), (float)(cy * inv), (float)(cz * inv));
    }

    private static Vector3 ReadVector(MatIndexer<float> idx, int row) => new(idx[row, 0], idx[row, 1], idx[row, 2]);

    private static void WriteVector(MatIndexer<float> idx, int row, Vector3 value)
    {
        idx[row, 0] = value.X;
        idx[row, 1] = value.Y;
        idx[row, 2] = value.Z;
    }
}

