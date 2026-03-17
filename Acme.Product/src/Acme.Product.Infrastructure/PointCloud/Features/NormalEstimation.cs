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
    /// Returns a new Nx3 CV_32FC1 Mat.
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

        return normals;
    }
}

