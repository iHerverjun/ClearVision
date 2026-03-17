using System.Numerics;
using System.Runtime.InteropServices;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Filters;

/// <summary>
/// Voxel grid downsampling for point clouds. Keeps one representative point per voxel (centroid).
/// </summary>
public sealed class VoxelGridFilter
{
    private readonly MatPool _pool;

    public VoxelGridFilter(MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
    }

    public PointCloud Downsample(PointCloud input, float leafSize)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (leafSize <= 0 || float.IsNaN(leafSize) || float.IsInfinity(leafSize))
        {
            throw new ArgumentOutOfRangeException(nameof(leafSize), "leafSize must be a positive finite number.");
        }

        if (input.Count == 0)
        {
            return new PointCloud(new Mat(0, 3, MatType.CV_32FC1), input.Colors != null ? new Mat(0, 3, MatType.CV_8UC1) : null, input.Normals != null ? new Mat(0, 3, MatType.CV_32FC1) : null, isOrganized: false, pool: _pool);
        }

        var invLeaf = 1f / leafSize;
        var hasColor = input.Colors != null;
        var hasNormals = input.Normals != null;

        var pIdx = input.Points.GetGenericIndexer<float>();
        var cIdx = input.Colors?.GetGenericIndexer<byte>();
        var nIdx = input.Normals?.GetGenericIndexer<float>();

        var voxels = new Dictionary<VoxelKey, Accumulator>(capacity: Math.Min(input.Count, 1_000_000));

        for (int i = 0; i < input.Count; i++)
        {
            var x = pIdx[i, 0];
            var y = pIdx[i, 1];
            var z = pIdx[i, 2];

            var key = new VoxelKey(
                (int)MathF.Floor(x * invLeaf),
                (int)MathF.Floor(y * invLeaf),
                (int)MathF.Floor(z * invLeaf));

            ref var acc = ref CollectionsMarshal.GetValueRefOrAddDefault(voxels, key, out _);
            acc.SumX += x;
            acc.SumY += y;
            acc.SumZ += z;
            acc.Count++;

            if (hasColor && cIdx != null)
            {
                acc.SumR += cIdx[i, 0];
                acc.SumG += cIdx[i, 1];
                acc.SumB += cIdx[i, 2];
            }

            if (hasNormals && nIdx != null)
            {
                acc.SumNx += nIdx[i, 0];
                acc.SumNy += nIdx[i, 1];
                acc.SumNz += nIdx[i, 2];
            }
        }

        var outCount = voxels.Count;
        if (outCount == 0)
        {
            return new PointCloud(new Mat(0, 3, MatType.CV_32FC1), hasColor ? new Mat(0, 3, MatType.CV_8UC1) : null, hasNormals ? new Mat(0, 3, MatType.CV_32FC1) : null, isOrganized: false, pool: _pool);
        }

        var outPoints = _pool.Rent(width: 3, height: outCount, type: MatType.CV_32FC1);
        var outColors = hasColor ? _pool.Rent(width: 3, height: outCount, type: MatType.CV_8UC1) : null;
        var outNormals = hasNormals ? _pool.Rent(width: 3, height: outCount, type: MatType.CV_32FC1) : null;

        var outP = outPoints.GetGenericIndexer<float>();
        var outC = outColors?.GetGenericIndexer<byte>();
        var outN = outNormals?.GetGenericIndexer<float>();

        int row = 0;
        foreach (var (_, acc) in voxels)
        {
            var inv = 1.0 / Math.Max(1, acc.Count);

            var cx = (float)(acc.SumX * inv);
            var cy = (float)(acc.SumY * inv);
            var cz = (float)(acc.SumZ * inv);

            outP[row, 0] = cx;
            outP[row, 1] = cy;
            outP[row, 2] = cz;

            if (outC != null)
            {
                outC[row, 0] = (byte)Math.Clamp((int)Math.Round(acc.SumR * inv), 0, 255);
                outC[row, 1] = (byte)Math.Clamp((int)Math.Round(acc.SumG * inv), 0, 255);
                outC[row, 2] = (byte)Math.Clamp((int)Math.Round(acc.SumB * inv), 0, 255);
            }

            if (outN != null)
            {
                var nn = new Vector3((float)(acc.SumNx * inv), (float)(acc.SumNy * inv), (float)(acc.SumNz * inv));
                if (nn.LengthSquared() > 1e-20f)
                {
                    nn = Vector3.Normalize(nn);
                }
                outN[row, 0] = nn.X;
                outN[row, 1] = nn.Y;
                outN[row, 2] = nn.Z;
            }

            row++;
        }

        return new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool);
    }

    private readonly record struct VoxelKey(int X, int Y, int Z)
    {
        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = (h * 31) + X;
                h = (h * 31) + Y;
                h = (h * 31) + Z;
                return h;
            }
        }
    }

    private struct Accumulator
    {
        public long Count;
        public double SumX;
        public double SumY;
        public double SumZ;

        public long SumR;
        public long SumG;
        public long SumB;

        public double SumNx;
        public double SumNy;
        public double SumNz;
    }
}
