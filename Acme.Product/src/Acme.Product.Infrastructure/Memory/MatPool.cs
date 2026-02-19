// MatPool.cs
// OpenCvSharp.Mat 的分桶内存池 - Sprint 1 Task 1.1
// 按图像规格分桶管理，避免高频堆申请导致的非托管内存碎片化
// 作者：蘅芜君

using System.Collections.Concurrent;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Memory;

/// <summary>
/// OpenCvSharp.Mat 的分桶内存池。
/// 按图像规格（宽、高、像素类型）分桶缓存已释放的 Mat 内存，
/// 供下次相同规格的 CoW 操作复用，避免向 OS 重复申请大块非托管内存。
/// 线程安全。
/// </summary>
public sealed class MatPool : IDisposable
{
    // 每个桶：key = 图像规格描述符，value = 可复用的空闲 Mat 队列
    private readonly ConcurrentDictionary<MatSpec, ConcurrentBag<Mat>> _buckets = new();

    // 每个桶的最大缓存数量，防止池无限膨胀（默认每种规格缓存不超过 8 个）
    private readonly int _maxPerBucket;

    // 内存池总容量上限（字节），超过后触发全局 Trim（清除最旧的桶）
    private readonly long _maxTotalBytes;

    private long _currentTotalBytes = 0;
    private bool _disposed = false;

    // 全局共享实例
    public static readonly MatPool Shared = new(maxPerBucket: 8, maxTotalGb: 2.0);

    public MatPool(int maxPerBucket = 8, double maxTotalGb = 2.0)
    {
        _maxPerBucket = maxPerBucket;
        _maxTotalBytes = (long)(maxTotalGb * 1024 * 1024 * 1024);
    }

    /// <summary>
    /// 从池中租用一个与目标规格相同的空白 Mat。
    /// 如果池中无可用缓冲块，则新建一个（向 OS 申请内存）。
    /// 租用的 Mat 内容未初始化，调用方负责填充数据后再使用。
    /// </summary>
    public Mat Rent(int width, int height, MatType type)
    {
        var spec = new MatSpec(width, height, type);

        if (_buckets.TryGetValue(spec, out var bag) && bag.TryTake(out var mat))
        {
            Interlocked.Add(ref _currentTotalBytes, -spec.ByteSize);

            // 验证 Mat 是否仍然有效
            if (!mat.IsDisposed && mat.Width == width && mat.Height == height && mat.Type() == type)
            {
                return mat; // 复用已有缓冲块，零 malloc
            }

            // Mat 已失效，直接释放
            mat.Dispose();
        }

        // 池中无缓存，只好新建（冷启动或池被耗尽时才发生）
        return new Mat(height, width, type);
    }

    /// <summary>
    /// 将一个不再使用的 Mat 归还池中供后续复用。
    /// 如果池已满或总内存超限，则直接 Dispose（向 OS 释放内存）。
    /// </summary>
    public void Return(Mat mat)
    {
        if (_disposed || mat.IsDisposed)
        {
            mat.Dispose();
            return;
        }

        var spec = new MatSpec(mat.Width, mat.Height, mat.Type());
        long wouldBe = Interlocked.Add(ref _currentTotalBytes, spec.ByteSize);

        // 超过总容量上限：不归还，直接释放
        if (wouldBe > _maxTotalBytes)
        {
            Interlocked.Add(ref _currentTotalBytes, -spec.ByteSize);
            mat.Dispose();
            return;
        }

        var bag = _buckets.GetOrAdd(spec, _ => new ConcurrentBag<Mat>());

        // 桶已满：不归还，直接释放
        if (bag.Count >= _maxPerBucket)
        {
            Interlocked.Add(ref _currentTotalBytes, -spec.ByteSize);
            mat.Dispose();
            return;
        }

        bag.Add(mat); // 入池
    }

    /// <summary>
    /// 收缩池（可在系统空闲时调用，或在内存压力回调中触发）。
    /// 释放所有缓存的 Mat，将非托管内存归还 OS。
    /// </summary>
    public void Trim()
    {
        foreach (var (_, bag) in _buckets)
        {
            while (bag.TryTake(out var mat))
                mat.Dispose();
        }
        Interlocked.Exchange(ref _currentTotalBytes, 0);
    }

    /// <summary>
    /// 获取当前池中的内存使用量（字节）
    /// </summary>
    public long CurrentTotalBytes => Interlocked.Read(ref _currentTotalBytes);

    /// <summary>
    /// 获取当前桶的数量
    /// </summary>
    public int BucketCount => _buckets.Count;

    public void Dispose()
    {
        _disposed = true;
        Trim();
    }

    /// <summary>图像规格描述符，作为分桶 Key</summary>
    private readonly record struct MatSpec(int Width, int Height, MatType Type)
    {
        // Depth: 0=CV_8U, 1=CV_8S, 2=CV_16U, 3=CV_16S, 4=CV_32S, 5=CV_32F, 6=CV_64F
        private static readonly int[] DepthBytes = { 1, 1, 2, 2, 4, 4, 8 };
        public long ByteSize => (long)Width * Height * Type.Channels * DepthBytes[Math.Min(Type.Depth, 6)];
    }
}
