using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Filters;

/// <summary>
/// Statistical Outlier Removal (SOR) for point clouds.
/// Brute-force KNN version intended for modest point counts (e.g. &lt;= 100k).
/// </summary>
public sealed class StatisticalOutlierRemoval
{
    private readonly MatPool _pool;

    public StatisticalOutlierRemoval(MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
    }

    public PointCloud Filter(PointCloud input, int meanK = 50, double stddevMul = 1.0)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (meanK <= 0) throw new ArgumentOutOfRangeException(nameof(meanK), "meanK must be a positive integer.");
        if (!double.IsFinite(stddevMul) || stddevMul < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stddevMul), "stddevMul must be a non-negative finite number.");
        }

        var n = input.Count;
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

        if (n == 1)
        {
            return CopyAll(input);
        }

        var k = Math.Min(meanK, n - 1);
        var pIdx = input.Points.GetGenericIndexer<float>();

        var meanDists = new double[n];
        var heap = new MaxHeap(capacity: k);

        for (int i = 0; i < n; i++)
        {
            heap.Clear();

            var xi = pIdx[i, 0];
            var yi = pIdx[i, 1];
            var zi = pIdx[i, 2];

            for (int j = 0; j < n; j++)
            {
                if (j == i)
                {
                    continue;
                }

                var dx = (double)pIdx[j, 0] - xi;
                var dy = (double)pIdx[j, 1] - yi;
                var dz = (double)pIdx[j, 2] - zi;

                var d2 = (dx * dx) + (dy * dy) + (dz * dz);
                if (!double.IsFinite(d2))
                {
                    continue;
                }

                heap.TryAdd(d2);
            }

            if (heap.Count == 0)
            {
                // Degenerate input (NaNs/Infs). Mark as outlier without poisoning global stats.
                meanDists[i] = double.PositiveInfinity;
                continue;
            }

            double sum = 0;
            for (int t = 0; t < heap.Count; t++)
            {
                sum += Math.Sqrt(heap.Items[t]);
            }
            meanDists[i] = sum / heap.Count;
        }

        // Compute global mean and std (population) with Welford, ignoring non-finite values.
        int m = 0;
        double mean = 0;
        double m2 = 0;
        for (int i = 0; i < n; i++)
        {
            var x = meanDists[i];
            if (!double.IsFinite(x))
            {
                continue;
            }

            m++;
            var delta = x - mean;
            mean += delta / m;
            var delta2 = x - mean;
            m2 += delta * delta2;
        }

        if (m == 0)
        {
            return new PointCloud(
                new Mat(0, 3, MatType.CV_32FC1),
                hasColors ? new Mat(0, 3, MatType.CV_8UC1) : null,
                hasNormals ? new Mat(0, 3, MatType.CV_32FC1) : null,
                isOrganized: false,
                pool: _pool);
        }

        var variance = m2 / m;
        var std = Math.Sqrt(Math.Max(0, variance));
        var threshold = mean + (stddevMul * std);

        var keep = new List<int>(capacity: Math.Min(n, 4096));
        for (int i = 0; i < n; i++)
        {
            var d = meanDists[i];
            if (!double.IsFinite(d))
            {
                continue;
            }

            if (d <= threshold)
            {
                keep.Add(i);
            }
        }

        if (keep.Count == 0)
        {
            return new PointCloud(
                new Mat(0, 3, MatType.CV_32FC1),
                hasColors ? new Mat(0, 3, MatType.CV_8UC1) : null,
                hasNormals ? new Mat(0, 3, MatType.CV_32FC1) : null,
                isOrganized: false,
                pool: _pool);
        }

        if (keep.Count == n)
        {
            return CopyAll(input);
        }

        var outPoints = _pool.Rent(width: 3, height: keep.Count, type: MatType.CV_32FC1);
        var outColors = hasColors ? _pool.Rent(width: 3, height: keep.Count, type: MatType.CV_8UC1) : null;
        var outNormals = hasNormals ? _pool.Rent(width: 3, height: keep.Count, type: MatType.CV_32FC1) : null;

        var outP = outPoints.GetGenericIndexer<float>();
        var outC = outColors?.GetGenericIndexer<byte>();
        var outN = outNormals?.GetGenericIndexer<float>();

        var cIdx = input.Colors?.GetGenericIndexer<byte>();
        var nIdx = input.Normals?.GetGenericIndexer<float>();

        for (int r = 0; r < keep.Count; r++)
        {
            var src = keep[r];

            outP[r, 0] = pIdx[src, 0];
            outP[r, 1] = pIdx[src, 1];
            outP[r, 2] = pIdx[src, 2];

            if (outC != null && cIdx != null)
            {
                outC[r, 0] = cIdx[src, 0];
                outC[r, 1] = cIdx[src, 1];
                outC[r, 2] = cIdx[src, 2];
            }

            if (outN != null && nIdx != null)
            {
                outN[r, 0] = nIdx[src, 0];
                outN[r, 1] = nIdx[src, 1];
                outN[r, 2] = nIdx[src, 2];
            }
        }

        return new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool);
    }

    private PointCloud CopyAll(PointCloud input)
    {
        var n = input.Count;
        var hasColors = input.Colors != null;
        var hasNormals = input.Normals != null;

        var points = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        input.Points.CopyTo(points);

        Mat? colors = null;
        if (hasColors && input.Colors != null)
        {
            colors = _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1);
            input.Colors.CopyTo(colors);
        }

        Mat? normals = null;
        if (hasNormals && input.Normals != null)
        {
            normals = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
            input.Normals.CopyTo(normals);
        }

        return new PointCloud(points, colors, normals, isOrganized: false, pool: _pool);
    }

    private sealed class MaxHeap
    {
        private readonly double[] _items;

        public int Count { get; private set; }
        public Span<double> Items => _items.AsSpan(0, Count);

        public MaxHeap(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new double[capacity];
        }

        public void Clear()
        {
            Count = 0;
        }

        public void TryAdd(double value)
        {
            if (Count < _items.Length)
            {
                _items[Count] = value;
                SiftUp(Count);
                Count++;
                return;
            }

            // Heap is full: only keep values smaller than current max (root).
            if (value >= _items[0])
            {
                return;
            }

            _items[0] = value;
            SiftDown(0);
        }

        private void SiftUp(int index)
        {
            var i = index;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_items[parent] >= _items[i])
                {
                    break;
                }

                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        private void SiftDown(int index)
        {
            var i = index;
            while (true)
            {
                var left = (2 * i) + 1;
                if (left >= Count)
                {
                    return;
                }

                var right = left + 1;
                var largest = left;
                if (right < Count && _items[right] > _items[left])
                {
                    largest = right;
                }

                if (_items[i] >= _items[largest])
                {
                    return;
                }

                (_items[i], _items[largest]) = (_items[largest], _items[i]);
                i = largest;
            }
        }
    }
}

