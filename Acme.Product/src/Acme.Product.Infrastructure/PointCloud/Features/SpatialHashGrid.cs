using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Features;

internal sealed class SpatialHashGridIndex
{
    public float CellSize { get; }
    public float InvCellSize { get; }
    public Dictionary<SpatialCellKey, List<int>> Cells { get; }

    public SpatialHashGridIndex(float cellSize, Dictionary<SpatialCellKey, List<int>> cells)
    {
        CellSize = cellSize;
        InvCellSize = 1f / cellSize;
        Cells = cells;
    }
}

internal static class SpatialHashGrid
{
    public static SpatialHashGridIndex Build(MatIndexer<float> points, int n, float cellSize)
    {
        var inv = 1f / cellSize;
        var cells = new Dictionary<SpatialCellKey, List<int>>(capacity: Math.Min(n, 1_000_000));

        for (int i = 0; i < n; i++)
        {
            var x = points[i, 0];
            var y = points[i, 1];
            var z = points[i, 2];

            var key = new SpatialCellKey(
                (int)MathF.Floor(x * inv),
                (int)MathF.Floor(y * inv),
                (int)MathF.Floor(z * inv));

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(cells, key, out var exists);
            if (!exists || list == null)
            {
                list = new List<int>(capacity: 8);
            }

            list.Add(i);
        }

        return new SpatialHashGridIndex(cellSize, cells);
    }

    public static void CollectRadiusNeighbors(
        MatIndexer<float> points,
        int index,
        SpatialHashGridIndex grid,
        float radius,
        double radiusSquared,
        List<int> output)
    {
        var inv = grid.InvCellSize;
        var x = points[index, 0];
        var y = points[index, 1];
        var z = points[index, 2];

        var centerKey = new SpatialCellKey(
            (int)MathF.Floor(x * inv),
            (int)MathF.Floor(y * inv),
            (int)MathF.Floor(z * inv));

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = new SpatialCellKey(centerKey.X + dx, centerKey.Y + dy, centerKey.Z + dz);
                    if (!grid.Cells.TryGetValue(key, out var candidates))
                    {
                        continue;
                    }

                    for (int c = 0; c < candidates.Count; c++)
                    {
                        var j = candidates[c];
                        if (j == index)
                        {
                            continue;
                        }

                        var ddx = (double)points[j, 0] - x;
                        var ddy = (double)points[j, 1] - y;
                        var ddz = (double)points[j, 2] - z;
                        var d2 = (ddx * ddx) + (ddy * ddy) + (ddz * ddz);
                        if (d2 <= radiusSquared)
                        {
                            output.Add(j);
                        }
                    }
                }
            }
        }
    }
}

internal readonly record struct SpatialCellKey(int X, int Y, int Z)
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
