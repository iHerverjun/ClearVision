// PipelineFrame.cs
// 获取元数据值
// 作者：蘅芜君

using OpenCvSharp;
using System.Diagnostics;

namespace Acme.Product.Infrastructure.Models;

/// <summary>
/// 流水线帧数据载体 - P3阶段O3.3流水线并行优化
/// 支持零拷贝传递和引用计数管理
/// </summary>
public class PipelineFrame : IDisposable
{
    private int _referenceCount = 1;
    private bool _disposed;

    /// <summary>
    /// 帧唯一标识
    /// </summary>
    public Guid FrameId { get; }

    /// <summary>
    /// 时间戳（Stopwatch ticks）
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// 原始图像数据（PNG编码）
    /// </summary>
    public byte[] RawImage { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 解码后的Mat对象（延迟解码，可空）
    /// </summary>
    public Mat? DecodedMat { get; set; }

    /// <summary>
    /// 元数据字典（宽度、高度、通道数等）
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 创建新的PipelineFrame
    /// </summary>
    public PipelineFrame()
    {
        FrameId = Guid.NewGuid();
        Timestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// 从现有数据创建PipelineFrame
    /// </summary>
    public PipelineFrame(byte[] rawImage, Dictionary<string, object>? metadata = null)
    {
        FrameId = Guid.NewGuid();
        Timestamp = Stopwatch.GetTimestamp();
        RawImage = rawImage ?? Array.Empty<byte>();
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// 尝试增加引用计数
    /// </summary>
    /// <returns>如果成功返回true，如果对象已释放返回false</returns>
    public bool TryAcquire()
    {
        var count = Interlocked.Increment(ref _referenceCount);
        if (count <= 1) // 已经被释放
        {
            Interlocked.Decrement(ref _referenceCount);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 释放引用（引用计数归零时释放资源）
    /// </summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref _referenceCount) == 0)
        {
            Dispose();
        }
    }

    /// <summary>
    /// 获取当前引用计数
    /// </summary>
    public int ReferenceCount => _referenceCount;

    /// <summary>
    /// 获取图像宽度（从Metadata或延迟解码）
    /// </summary>
    public int GetWidth()
    {
        if (Metadata.TryGetValue("Width", out var w) && w is int width)
            return width;
        
        // 尝试从PNG头部解析
        var dims = TryParsePngDimensions(RawImage);
        if (dims.HasValue)
        {
            Metadata["Width"] = dims.Value.width;
            return dims.Value.width;
        }

        // 延迟解码
        EnsureDecoded();
        return DecodedMat?.Width ?? 0;
    }

    /// <summary>
    /// 获取图像高度
    /// </summary>
    public int GetHeight()
    {
        if (Metadata.TryGetValue("Height", out var h) && h is int height)
            return height;
        
        var dims = TryParsePngDimensions(RawImage);
        if (dims.HasValue)
        {
            Metadata["Height"] = dims.Value.height;
            return dims.Value.height;
        }

        EnsureDecoded();
        return DecodedMat?.Height ?? 0;
    }

    /// <summary>
    /// 获取通道数
    /// </summary>
    public int GetChannels()
    {
        if (Metadata.TryGetValue("Channels", out var c) && c is int ch)
            return ch;

        EnsureDecoded();
        return DecodedMat?.Channels() ?? 3;
    }

    /// <summary>
    /// 确保Mat已解码（延迟解码）
    /// </summary>
    public void EnsureDecoded()
    {
        if (DecodedMat != null && !DecodedMat.IsDisposed)
            return;

        if (RawImage.Length > 0)
        {
            DecodedMat = Cv2.ImDecode(RawImage, ImreadModes.Color);
        }
    }

    /// <summary>
    /// 从PNG头部快速解析尺寸
    /// </summary>
    private static (int width, int height)? TryParsePngDimensions(byte[] pngData)
    {
        if (pngData == null || pngData.Length < 24)
            return null;

        // PNG签名: 89 50 4E 47 0D 0A 1A 0A
        if (pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47)
            return null;

        // IHDR chunk 从第16字节开始
        int width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
        int height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];

        return (width, height);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        DecodedMat?.Dispose();
        DecodedMat = null;
        RawImage = Array.Empty<byte>();
        Metadata.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 终结器
    /// </summary>
    ~PipelineFrame()
    {
        Dispose();
    }

    /// <summary>
    /// 创建帧的副本（深拷贝RawImage，引用计数独立）
    /// </summary>
    public PipelineFrame Clone()
    {
        var clone = new PipelineFrame
        {
            RawImage = RawImage.ToArray(), // 深拷贝
            Metadata = new Dictionary<string, object>(Metadata)
        };

        if (DecodedMat != null && !DecodedMat.IsDisposed)
        {
            clone.DecodedMat = DecodedMat.Clone();
        }

        return clone;
    }

    /// <summary>
    /// 获取执行耗时（毫秒）
    /// </summary>
    public double GetElapsedMs()
    {
        var current = Stopwatch.GetTimestamp();
        var elapsedTicks = current - Timestamp;
        return (double)elapsedTicks / Stopwatch.Frequency * 1000;
    }

    /// <summary>
    /// 添加或更新元数据
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        Metadata[key] = value;
    }

    /// <summary>
    /// 获取元数据值
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }
}
