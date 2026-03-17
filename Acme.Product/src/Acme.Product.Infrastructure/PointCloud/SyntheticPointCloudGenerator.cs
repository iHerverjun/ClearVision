using System.Globalization;
using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud;

/// <summary>
/// Phase-2 synthetic point cloud generator.
/// Generates plane/sphere/cylinder/cube samples with optional Gaussian noise, colors, normals and outliers.
/// </summary>
public sealed class SyntheticPointCloudGenerator
{
    private readonly Random _rng;
    private readonly MatPool _pool;

    public SyntheticPointCloudGenerator(int seed = 12345, MatPool? pool = null)
    {
        _rng = new Random(seed);
        _pool = pool ?? MatPool.Shared;
    }

    public PointCloud GeneratePlane(
        Vector3 center,
        Vector3 normal,
        (float Width, float Height) size,
        int density,
        float noise,
        bool includeColors = true,
        bool includeNormals = true,
        float outlierRatio = 0.0f)
    {
        normal = SafeNormalize(normal, fallback: Vector3.UnitZ);
        BuildPlaneBasis(normal, out var u, out var v);

        var n = Math.Max(density, 1);
        var points = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var colors = includeColors ? _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1) : null;
        var normals = includeNormals ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;

        var pIdx = points.GetGenericIndexer<float>();
        var cIdx = colors?.GetGenericIndexer<byte>();
        var nIdx = normals?.GetGenericIndexer<float>();

        for (int i = 0; i < n; i++)
        {
            var a = NextUniform(-size.Width / 2f, size.Width / 2f);
            var b = NextUniform(-size.Height / 2f, size.Height / 2f);
            var pos = center + (u * a) + (v * b);
            pos += NextGaussian3(noise);

            pIdx[i, 0] = pos.X;
            pIdx[i, 1] = pos.Y;
            pIdx[i, 2] = pos.Z;

            if (nIdx != null)
            {
                nIdx[i, 0] = normal.X;
                nIdx[i, 1] = normal.Y;
                nIdx[i, 2] = normal.Z;
            }

            if (cIdx != null)
            {
                // Slight gradient on u direction to help visualize orientation.
                var t = (a / Math.Max(1e-6f, size.Width)) + 0.5f;
                var r = (byte)Math.Clamp((int)(40 + 180 * t), 0, 255);
                cIdx[i, 0] = r;
                cIdx[i, 1] = 140;
                cIdx[i, 2] = 220;
            }
        }

        var cloud = new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
        return outlierRatio > 0 ? AddOutliers(cloud, outlierRatio) : cloud;
    }

    public PointCloud GenerateSphere(
        Vector3 center,
        float radius,
        int numPoints,
        float noise,
        bool includeColors = true,
        bool includeNormals = true,
        float outlierRatio = 0.0f)
    {
        var n = Math.Max(numPoints, 1);
        var points = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var colors = includeColors ? _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1) : null;
        var normals = includeNormals ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;

        var pIdx = points.GetGenericIndexer<float>();
        var cIdx = colors?.GetGenericIndexer<byte>();
        var nIdx = normals?.GetGenericIndexer<float>();

        for (int i = 0; i < n; i++)
        {
            // Uniform on sphere surface: cos(theta) ~ U[-1,1], phi ~ U[0,2pi)
            var u = NextUniform(-1f, 1f);
            var phi = NextUniform(0f, 2f * MathF.PI);
            var s = MathF.Sqrt(MathF.Max(0f, 1f - (u * u)));

            var dir = new Vector3(
                s * MathF.Cos(phi),
                s * MathF.Sin(phi),
                u);

            var pos = center + (dir * radius) + NextGaussian3(noise);

            pIdx[i, 0] = pos.X;
            pIdx[i, 1] = pos.Y;
            pIdx[i, 2] = pos.Z;

            if (nIdx != null)
            {
                var nn = SafeNormalize(pos - center, fallback: dir);
                nIdx[i, 0] = nn.X;
                nIdx[i, 1] = nn.Y;
                nIdx[i, 2] = nn.Z;
            }

            if (cIdx != null)
            {
                // Map normal-ish to RGB for visualization.
                var nn = SafeNormalize(pos - center, fallback: dir);
                cIdx[i, 0] = (byte)Math.Clamp((int)(127 + 127 * nn.X), 0, 255);
                cIdx[i, 1] = (byte)Math.Clamp((int)(127 + 127 * nn.Y), 0, 255);
                cIdx[i, 2] = (byte)Math.Clamp((int)(127 + 127 * nn.Z), 0, 255);
            }
        }

        var cloud = new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
        return outlierRatio > 0 ? AddOutliers(cloud, outlierRatio) : cloud;
    }

    public PointCloud GenerateCylinder(
        Vector3 center,
        Vector3 axis,
        float radius,
        float height,
        int numPoints,
        float noise,
        bool includeColors = true,
        bool includeNormals = true,
        float outlierRatio = 0.0f)
    {
        axis = SafeNormalize(axis, fallback: Vector3.UnitZ);
        BuildPlaneBasis(axis, out var u, out var v);

        var n = Math.Max(numPoints, 1);
        var points = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var colors = includeColors ? _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1) : null;
        var normals = includeNormals ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;

        var pIdx = points.GetGenericIndexer<float>();
        var cIdx = colors?.GetGenericIndexer<byte>();
        var nIdx = normals?.GetGenericIndexer<float>();

        var halfH = height / 2f;
        for (int i = 0; i < n; i++)
        {
            var theta = NextUniform(0f, 2f * MathF.PI);
            var z = NextUniform(-halfH, halfH);

            var radial = (u * MathF.Cos(theta)) + (v * MathF.Sin(theta));
            var pos = center + (axis * z) + (radial * radius) + NextGaussian3(noise);

            pIdx[i, 0] = pos.X;
            pIdx[i, 1] = pos.Y;
            pIdx[i, 2] = pos.Z;

            if (nIdx != null)
            {
                // For a perfect cylinder, the normal is the radial direction (ignoring noise).
                var nn = SafeNormalize(radial, fallback: radial);
                nIdx[i, 0] = nn.X;
                nIdx[i, 1] = nn.Y;
                nIdx[i, 2] = nn.Z;
            }

            if (cIdx != null)
            {
                cIdx[i, 0] = 240;
                cIdx[i, 1] = (byte)Math.Clamp((int)(127 + 127 * (z / Math.Max(1e-6f, halfH))), 0, 255);
                cIdx[i, 2] = 60;
            }
        }

        var cloud = new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
        return outlierRatio > 0 ? AddOutliers(cloud, outlierRatio) : cloud;
    }

    public PointCloud GenerateCube(
        Vector3 center,
        float edgeLength,
        int numPoints,
        float noise,
        bool includeColors = true,
        bool includeNormals = true,
        float outlierRatio = 0.0f)
    {
        var n = Math.Max(numPoints, 1);
        var points = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var colors = includeColors ? _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1) : null;
        var normals = includeNormals ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;

        var pIdx = points.GetGenericIndexer<float>();
        var cIdx = colors?.GetGenericIndexer<byte>();
        var nIdx = normals?.GetGenericIndexer<float>();

        var half = edgeLength / 2f;
        for (int i = 0; i < n; i++)
        {
            // Sample a random face.
            var face = _rng.Next(6);
            float a = NextUniform(-half, half);
            float b = NextUniform(-half, half);

            Vector3 normal;
            Vector3 pos = face switch
            {
                0 => new Vector3(half, a, b),  // +X
                1 => new Vector3(-half, a, b), // -X
                2 => new Vector3(a, half, b),  // +Y
                3 => new Vector3(a, -half, b), // -Y
                4 => new Vector3(a, b, half),  // +Z
                _ => new Vector3(a, b, -half), // -Z
            };

            normal = face switch
            {
                0 => Vector3.UnitX,
                1 => -Vector3.UnitX,
                2 => Vector3.UnitY,
                3 => -Vector3.UnitY,
                4 => Vector3.UnitZ,
                _ => -Vector3.UnitZ
            };

            pos = center + pos + NextGaussian3(noise);

            pIdx[i, 0] = pos.X;
            pIdx[i, 1] = pos.Y;
            pIdx[i, 2] = pos.Z;

            if (nIdx != null)
            {
                nIdx[i, 0] = normal.X;
                nIdx[i, 1] = normal.Y;
                nIdx[i, 2] = normal.Z;
            }

            if (cIdx != null)
            {
                cIdx[i, 0] = (byte)Math.Clamp(80 + 30 * face, 0, 255);
                cIdx[i, 1] = (byte)Math.Clamp(210 - 20 * face, 0, 255);
                cIdx[i, 2] = 120;
            }
        }

        var cloud = new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
        return outlierRatio > 0 ? AddOutliers(cloud, outlierRatio) : cloud;
    }

    public PointCloud AddOutliers(PointCloud cloud, float outlierRatio, AxisAlignedBoundingBox? bounds = null)
    {
        if (outlierRatio <= 0)
        {
            return cloud;
        }

        var aabb = bounds ?? cloud.GetAABB();
        var baseCount = cloud.Count;
        var extra = (int)MathF.Ceiling(baseCount * outlierRatio);
        extra = Math.Clamp(extra, 1, Math.Max(1, baseCount));

        var total = baseCount + extra;
        var points = _pool.Rent(width: 3, height: total, type: MatType.CV_32FC1);
        var colors = cloud.Colors != null ? _pool.Rent(width: 3, height: total, type: MatType.CV_8UC1) : null;
        var normals = cloud.Normals != null ? _pool.Rent(width: 3, height: total, type: MatType.CV_32FC1) : null;

        // Copy original
        cloud.Points.CopyTo(points.RowRange(0, baseCount));
        if (colors != null && cloud.Colors != null)
        {
            cloud.Colors.CopyTo(colors.RowRange(0, baseCount));
        }
        if (normals != null && cloud.Normals != null)
        {
            cloud.Normals.CopyTo(normals.RowRange(0, baseCount));
        }

        // Outliers
        var pIdx = points.GetGenericIndexer<float>();
        var cIdx = colors?.GetGenericIndexer<byte>();
        var nIdx = normals?.GetGenericIndexer<float>();

        // Expand bounds a bit so outliers are clearly separable.
        var min = aabb.Min - (aabb.Extent * 0.25f) - new Vector3(0.01f);
        var max = aabb.Max + (aabb.Extent * 0.25f) + new Vector3(0.01f);

        for (int i = 0; i < extra; i++)
        {
            var r = baseCount + i;
            var pos = new Vector3(
                NextUniform(min.X, max.X),
                NextUniform(min.Y, max.Y),
                NextUniform(min.Z, max.Z));

            pIdx[r, 0] = pos.X;
            pIdx[r, 1] = pos.Y;
            pIdx[r, 2] = pos.Z;

            if (cIdx != null)
            {
                cIdx[r, 0] = 255;
                cIdx[r, 1] = 0;
                cIdx[r, 2] = 0;
            }

            if (nIdx != null)
            {
                nIdx[r, 0] = 0;
                nIdx[r, 1] = 0;
                nIdx[r, 2] = 0;
            }
        }

        cloud.Dispose();
        return new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
    }

    private static void BuildPlaneBasis(Vector3 normal, out Vector3 u, out Vector3 v)
    {
        // Pick a vector not parallel to normal.
        var a = Math.Abs(normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
        u = Vector3.Normalize(Vector3.Cross(a, normal));
        v = Vector3.Normalize(Vector3.Cross(normal, u));
    }

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        var len2 = v.LengthSquared();
        if (len2 < 1e-20f)
        {
            return fallback;
        }
        return Vector3.Normalize(v);
    }

    private float NextUniform(float min, float max) => min + ((max - min) * (float)_rng.NextDouble());

    private Vector3 NextGaussian3(float sigma)
    {
        if (sigma <= 0)
        {
            return Vector3.Zero;
        }

        return new Vector3(
            NextGaussian(0f, sigma),
            NextGaussian(0f, sigma),
            NextGaussian(0f, sigma));
    }

    private float NextGaussian(float mean, float stdDev)
    {
        // Box-Muller
        var u1 = 1.0 - _rng.NextDouble();
        var u2 = 1.0 - _rng.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return (float)(mean + (stdDev * randStdNormal));
    }
}
