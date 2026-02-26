// ImageWrapper.cs
// 引用计数 + 写时复制(CoW) + 分桶内存池集成 - Sprint 1 Task 1.1
// 解决 7×24 长期运行的内存稳定性问题：精确生命周期、并发数据隔离、恒定节拍内存
// 作者：蘅芜君

using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像数据包装器 - 引用计数 + 写时复制(CoW) + 分桶内存池
/// 
/// 核心设计：
/// 1. 引用计数(RC)：精确控制 Mat 生命周期，避免统一延迟释放导致的内存峰值
/// 2. 写时复制(CoW)：并发读取共享数据，写入时从内存池取缓冲块创建私有副本
/// 3. 分桶内存池(Pool)：CoW 触发时从池中取缓冲块，避免向 OS 高频申请大块非托管内存
/// 
/// 使用约定：
/// - 只读访问：使用 MatReadOnly 属性，不触发 CoW，多并发安全
/// - 写访问：使用 GetWritableMat()，可能触发 CoW，返回的 Mat 需用于构建新的 ImageWrapper
/// - 生命周期：通过 AddRef() 增加引用，通过 Release() 释放引用，计数归零时 Mat 归还内存池
/// </summary>
public sealed class ImageWrapper : IDisposable
{
    private Mat _mat = null!;
    private int _refCount = 1;
    private readonly object _lock = new();
    private bool _disposed = false;
    private byte[]? _bytes; // 延迟编码缓存

    // CoW 时从此池取缓冲块；归还时将废弃 Mat 放回池中
    // 允许外部注入不同的池实例（便于测试），默认使用全局共享池
    private readonly MatPool _pool;

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width => GetMat().Width;

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height => GetMat().Height;

    /// <summary>
    /// 通道数
    /// </summary>
    public int Channels => GetMat().Channels();

    /// <summary>
    /// 从 Mat 创建包装器
    /// </summary>
    /// <param name="mat">OpenCV Mat 对象，所有权转移给 ImageWrapper</param>
    /// <param name="pool">可选的内存池实例，默认使用全局共享池</param>
    public ImageWrapper(Mat mat, MatPool? pool = null)
    {
        _mat = mat ?? throw new ArgumentNullException(nameof(mat));
        _pool = pool ?? MatPool.Shared;
    }

    /// <summary>
    /// 从字节数组创建包装器（延迟解码）
    /// </summary>
    /// <param name="bytes">编码后的图像数据（PNG/JPEG等）</param>
    /// <param name="pool">可选的内存池实例，默认使用全局共享池</param>
    public ImageWrapper(byte[] bytes, MatPool? pool = null)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _pool = pool ?? MatPool.Shared;
    }

    /// <summary>
    /// 只读访问 —— 多个消费者可安全并发读取，不触发 CoW
    /// </summary>
    public Mat MatReadOnly
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return GetMat();
        }
    }

    /// <summary>
    /// 获取 Mat（如果只有 bytes 则延迟解码）
    /// </summary>
    public Mat GetMat()
    {
        if (_mat != null)
            return _mat;

        lock (_lock)
        {
            if (_mat != null)
                return _mat;

            if (_bytes == null)
            {
                throw new InvalidOperationException("图像数据为空，无法解码");
            }

            _mat = Cv2.ImDecode(_bytes, ImreadModes.Color);
            if (_mat == null || _mat.Empty())
            {
                throw new InvalidOperationException("图像解码失败，数据可能损坏或格式不支持");
            }

            return _mat;
        }
    }

    /// <summary>
    /// 写访问（CoW 核心接口）。
    ///
    /// 当 refCount > 1（有其他持有者）时，从内存池取一块相同规格的空白 Mat，
    /// 将当前数据 CopyTo（写入）新缓冲区，返回这块私有缓冲区供调用方就地修改。
    ///
    /// 关键点：使用 CopyTo 而非 Clone()，区别在于：
    ///   Clone()     = 由 OpenCV 内部向 OS 申请新内存
    ///   Pool.Rent() = 从池中取现有缓冲块，零 malloc（主路径）
    ///
    /// 返回的 Mat 不受 ImageWrapper 引用计数管理，由调用方负责：
    ///   - 就地修改后，用它构建一个新的 ImageWrapper(newMat) 作为输出
    ///   - 如果不需要了，调用 MatPool.Shared.Return(mat) 归还池
    /// </summary>
    public Mat GetWritableMat()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 快速路径：只有自己持有，直接返回原始 Mat，零拷贝、零开销
        if (Interlocked.CompareExchange(ref _refCount, 0, 0) == 1)
        {
            return _mat;
        }

        // 慢速路径：有其他持有者，执行 CoW
        var pooledMat = _pool.Rent(_mat.Width, _mat.Height, _mat.Type());

        try
        {
            _mat.CopyTo(pooledMat); // 数据拷贝（目标内存从池中取，无 malloc）
            return pooledMat;
        }
        catch
        {
            // 拷贝失败，归还缓冲块
            _pool.Return(pooledMat);
            throw;
        }
    }

    /// <summary>
    /// 增加引用计数。
    /// 当 DAG 中存在扇出（一个算子输出连接多个下游算子）时，
    /// 框架层对每个额外的下游调用一次 AddRef()。
    /// </summary>
    public ImageWrapper AddRef()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Interlocked.Increment(ref _refCount);
        }
        return this;
    }

    /// <summary>
    /// 释放引用。
    /// 当最后一个持有者调用 Release() 后，Mat 归还内存池（而非直接 Dispose）。
    /// </summary>
    public void Release()
    {
        int remaining = Interlocked.Decrement(ref _refCount);

        if (remaining == 0)
        {
            Dispose();
        }
        else if (remaining < 0)
        {
            // 引用计数为负，检测到双重释放 Bug
            Interlocked.Increment(ref _refCount);
            throw new InvalidOperationException(
                "[ImageWrapper] 引用计数为负，检测到双重释放 Bug");
        }
    }

    /// <summary>
    /// 获取当前引用计数（用于调试和测试）
    /// </summary>
    public int RefCount => Interlocked.CompareExchange(ref _refCount, 0, 0);

    /// <summary>
    /// 获取字节数组（仅在需要时编码，如保存/传输）
    /// 默认使用 PNG 格式编码
    /// </summary>
    public byte[] GetBytes()
    {
        if (_bytes != null)
            return _bytes;

        lock (_lock)
        {
            if (_bytes != null)
                return _bytes;

            var mat = GetMat();
            _bytes = mat.ToBytes(".png");
            return _bytes;
        }
    }

    /// <summary>
    /// 获取字节数组（指定编码格式）
    /// </summary>
    public byte[] GetBytes(string extension)
    {
        return GetMat().ToBytes(extension);
    }

    /// <summary>
    /// 转换为字节数组（显式编码，缓存结果）
    /// </summary>
    public byte[] ToBytes()
    {
        return GetBytes();
    }

    /// <summary>
    /// 检查是否已解码
    /// </summary>
    public bool IsDecoded => _mat != null;

    /// <summary>
    /// 释放资源 —— 将 Mat 归还内存池（而非直接 Dispose，实现内存复用）
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;

            // 将 Mat 归还内存池（实现内存复用）
            if (_mat != null && !_mat.IsDisposed)
            {
                _pool.Return(_mat);
            }

            _mat = null!;
            _bytes = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 从字节数组创建包装器（静态工厂方法）
    /// </summary>
    public static ImageWrapper FromBytes(byte[] bytes, MatPool? pool = null)
    {
        return new ImageWrapper(bytes, pool);
    }

    /// <summary>
    /// 从对象尝试获取图像数据
    /// 支持 ImageWrapper、byte[] 或 Mat 类型
    /// </summary>
    public static bool TryGetFromObject(object? value, out ImageWrapper? image)
    {
        image = null;

        if (value == null)
            return false;

        if (value is ImageWrapper wrapper)
        {
            image = wrapper;
            return true;
        }

        if (value is byte[] bytes)
        {
            image = new ImageWrapper(bytes);
            return true;
        }

        if (value is Mat mat)
        {
            image = new ImageWrapper(mat);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从字典输入中提取图像
    /// </summary>
    public static bool TryGetFromInputs(Dictionary<string, object>? inputs, string key, out ImageWrapper? image)
    {
        image = null;

        if (inputs == null)
            return false;
        if (!inputs.TryGetValue(key, out var value))
            return false;

        return TryGetFromObject(value, out image);
    }

    /// <summary>
    /// 隐式转换：ImageWrapper -> byte[]
    /// 触发编码（如果需要）
    /// </summary>
    public static implicit operator byte[](ImageWrapper wrapper)
    {
        if (wrapper == null)
            throw new ArgumentNullException(nameof(wrapper));
        return wrapper.GetBytes();
    }

    /// <summary>
    /// 尝试从PNG数据头部快速解析图像尺寸（避免完整解码）
    /// </summary>
    public static (int width, int height, int channels)? TryParsePngDimensions(byte[] pngData)
    {
        if (pngData == null || pngData.Length < 24)
            return null;

        // PNG签名: 89 50 4E 47 0D 0A 1A 0A
        if (pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47)
            return null; // 不是PNG格式

        // IHDR chunk 从第16字节开始 (8字节签名 + 4字节长度 + 4字节"IHDR")
        // 宽度和高度是大端序 (Big Endian) 4字节整数
        int width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
        int height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];

        // 通道数：根据IHDR的Color Type推断（第25字节）
        int colorType = pngData.Length > 25 ? pngData[25] : 2;
        int channels = colorType switch
        {
            0 => 1,  // Grayscale
            2 => 3,  // RGB
            3 => 3,  // Indexed (palette)
            4 => 2,  // Gray + Alpha
            6 => 4,  // RGBA
            _ => 3   // 默认为RGB
        };

        return (width, height, channels);
    }
}
