// ImageWrapper.cs
// 隐式转换：ImageWrapper -> byte[]
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像数据包装器 - 延迟编码，直传 Mat 引用
/// 算子间传递时避免不必要的 PNG 编解码，仅在需要时执行
/// </summary>
public class ImageWrapper : IDisposable
{
    private Mat? _mat;
    private byte[]? _bytes;
    private readonly object _lock = new();
    private bool _disposed;

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
    /// <param name="mat">OpenCV Mat 对象，将被克隆以确保独立生命周期</param>
    public ImageWrapper(Mat mat)
    {
        _mat = mat.Clone();
    }

    /// <summary>
    /// 从字节数组创建包装器（延迟解码）
    /// </summary>
    /// <param name="bytes">编码后的图像数据（PNG/JPEG等）</param>
    public ImageWrapper(byte[] bytes)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
    }

    /// <summary>
    /// 获取 Mat（如果只有 bytes 则延迟解码）
    /// </summary>
    /// <returns>OpenCV Mat 对象</returns>
    /// <exception cref="InvalidOperationException">解码失败时抛出</exception>
    public Mat GetMat()
    {
        if (_mat != null) return _mat;

        lock (_lock)
        {
            if (_mat != null) return _mat;

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
    /// 获取字节数组（仅在需要时编码，如保存/传输）
    /// 默认使用 PNG 格式编码
    /// </summary>
    /// <returns>编码后的图像数据</returns>
    public byte[] GetBytes()
    {
        if (_bytes != null) return _bytes;

        lock (_lock)
        {
            if (_bytes != null) return _bytes;

            if (_mat == null)
            {
                throw new InvalidOperationException("图像数据为空，无法编码");
            }

            _bytes = _mat.ToBytes(".png");
            return _bytes;
        }
    }

    /// <summary>
    /// 获取字节数组（指定编码格式）
    /// </summary>
    /// <param name="extension">编码格式扩展名，如 ".png", ".jpg"</param>
    /// <returns>编码后的图像数据</returns>
    public byte[] GetBytes(string extension)
    {
        if (_mat == null)
        {
            throw new InvalidOperationException("图像数据为空，无法编码");
        }

        return _mat.ToBytes(extension);
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
    /// 检查是否已编码
    /// </summary>
    public bool IsEncoded => _bytes != null;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            _mat?.Dispose();
            _mat = null;
            _bytes = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 从字节数组创建包装器（静态工厂方法）
    /// </summary>
    /// <param name="bytes">编码后的图像数据</param>
    /// <returns>图像包装器</returns>
    public static ImageWrapper FromBytes(byte[] bytes)
    {
        return new ImageWrapper(bytes);
    }

    /// <summary>
    /// 尝试从PNG数据头部快速解析图像尺寸（避免完整解码）
    /// </summary>
    /// <param name="pngData">PNG格式字节数组</param>
    /// <returns>(width, height, channels) 元组，解析失败返回null</returns>
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
        // 0=Grayscale(1ch), 2=RGB(3ch), 3=Indexed(1ch+palette), 4=Gray+Alpha(2ch), 6=RGBA(4ch)
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

    /// <summary>
    /// 从对象尝试获取图像数据
    /// 支持 ImageWrapper、byte[] 或 Mat 类型
    /// </summary>
    /// <param name="value">输入对象</param>
    /// <param name="image">输出图像包装器</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGetFromObject(object? value, out ImageWrapper? image)
    {
        image = null;

        if (value == null) return false;

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
    /// <param name="inputs">输入字典</param>
    /// <param name="key">图像键名，默认为 "Image"</param>
    /// <param name="image">输出图像包装器</param>
    /// <returns>是否成功提取</returns>
    public static bool TryGetFromInputs(Dictionary<string, object>? inputs, string key, out ImageWrapper? image)
    {
        image = null;

        if (inputs == null) return false;
        if (!inputs.TryGetValue(key, out var value)) return false;

        return TryGetFromObject(value, out image);
    }

    /// <summary>
    /// 隐式转换：ImageWrapper -> byte[]
    /// 触发编码（如果需要）
    /// </summary>
    public static implicit operator byte[](ImageWrapper wrapper)
    {
        if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));
        return wrapper.GetBytes();
    }
}
