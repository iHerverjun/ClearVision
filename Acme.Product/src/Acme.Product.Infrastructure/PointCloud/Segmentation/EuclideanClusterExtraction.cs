using System.Numerics;
using System.Runtime.InteropServices;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Segmentation;

public sealed class EuclideanClusterExtraction
{
    private readonly MatPool _pool;

    public EuclideanClusterExtraction(MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
    }

    /// <summary>
    /// Extract clusters using Euclidean distance connectivity (3D BFS).
    /// </summary>
    public List<int[]> Extract(
        PointCloud cloud,
        float clusterTolerance = 0.02f,
        int minClusterSize = 100,
        int maxClusterSize = 1_000_000)
    {
        if (cloud == null) throw new ArgumentNullException(nameof(cloud));
        if (clusterTolerance <= 0 || !float.IsFinite(clusterTolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(clusterTolerance), "clusterTolerance must be positive and finite.");
        }
        if (minClusterSize <= 0) throw new ArgumentOutOfRangeException(nameof(minClusterSize), "minClusterSize must be > 0.");
        if (maxClusterSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxClusterSize), "maxClusterSize must be > 0.");
        if (minClusterSize > maxClusterSize)
        {
            throw new ArgumentOutOfRangeException(nameof(minClusterSize), "minClusterSize must be <= maxClusterSize.");
        }

        int n = cloud.Count;
        if (n == 0)
        {
            return new List<int[]>(capacity: 0);
        }

        var pIdx = cloud.Points.GetGenericIndexer<float>();
        var visited = new bool[n];

        // Grid-based neighbor search: bucket points by cell size = clusterTolerance.
        // For each point, only check points in its 27 neighboring cells.
        var inv = 1f / clusterTolerance;
        var grid = new Dictionary<CellKey, List<int>>(capacity: Math.Min(n, 1_000_000));

        for (int i = 0; i < n; i++)
        {
            var x = pIdx[i, 0];
            var y = pIdx[i, 1];
            var z = pIdx[i, 2];

            var key = new CellKey(
                (int)MathF.Floor(x * inv),
                (int)MathF.Floor(y * inv),
                (int)MathF.Floor(z * inv));

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(grid, key, out var exists);
            if (!exists || list == null)
            {
                list = new List<int>(capacity: 8);
            }

            list.Add(i);
        }

        var tol2 = (double)clusterTolerance * clusterTolerance;
        var clusters = new List<int[]>();
        var queue = new Queue<int>(capacity: Math.Min(n, 4096));
        var scratch = new List<int>(capacity: Math.Min(n, 4096));

        for (int i = 0; i < n; i++)
        {
            if (visited[i])
            {
                continue;
            }

            scratch.Clear();
            queue.Clear();
            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                scratch.Add(current);

                var cx = pIdx[current, 0];
                var cy = pIdx[current, 1];
                var cz = pIdx[current, 2];

                var cKey = new CellKey(
                    (int)MathF.Floor(cx * inv),
                    (int)MathF.Floor(cy * inv),
                    (int)MathF.Floor(cz * inv));

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var key = new CellKey(cKey.X + dx, cKey.Y + dy, cKey.Z + dz);
                            if (!grid.TryGetValue(key, out var candidates))
                            {
                                continue;
                            }

                            for (int c = 0; c < candidates.Count; c++)
                            {
                                var j = candidates[c];
                                if (visited[j])
                                {
                                    continue;
                                }

                                var ddx = (double)pIdx[j, 0] - cx;
                                var ddy = (double)pIdx[j, 1] - cy;
                                var ddz = (double)pIdx[j, 2] - cz;
                                var d2 = (ddx * ddx) + (ddy * ddy) + (ddz * ddz);
                                if (d2 <= tol2)
                                {
                                    visited[j] = true;
                                    queue.Enqueue(j);
                                }
                            }
                        }
                    }
                }
            }

            if (scratch.Count >= minClusterSize && scratch.Count <= maxClusterSize)
            {
                clusters.Add(scratch.ToArray());
            }
        }

        return clusters;
    }

    /// <summary>
    /// Convenience: materialize each cluster as a new point cloud.
    /// </summary>
    public List<PointCloud> ExtractPointClouds(
        PointCloud input,
        float clusterTolerance = 0.02f,
        int minClusterSize = 100,
        int maxClusterSize = 1_000_000)
    {
        var clusters = Extract(input, clusterTolerance, minClusterSize, maxClusterSize);
        var clouds = new List<PointCloud>(clusters.Count);

        var hasColors = input.Colors != null;
        var hasNormals = input.Normals != null;

        var srcP = input.Points.GetGenericIndexer<float>();
        var srcC = input.Colors?.GetGenericIndexer<byte>();
        var srcN = input.Normals?.GetGenericIndexer<float>();

        for (int ci = 0; ci < clusters.Count; ci++)
        {
            var indices = clusters[ci];
            var outPoints = _pool.Rent(width: 3, height: indices.Length, type: MatType.CV_32FC1);
            var outColors = hasColors ? _pool.Rent(width: 3, height: indices.Length, type: MatType.CV_8UC1) : null;
            var outNormals = hasNormals ? _pool.Rent(width: 3, height: indices.Length, type: MatType.CV_32FC1) : null;

            var dstP = outPoints.GetGenericIndexer<float>();
            var dstC = outColors?.GetGenericIndexer<byte>();
            var dstN = outNormals?.GetGenericIndexer<float>();

            for (int r = 0; r < indices.Length; r++)
            {
                var i = indices[r];
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

            clouds.Add(new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool));
        }

        return clouds;
    }

    private readonly record struct CellKey(int X, int Y, int Z)
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
}

