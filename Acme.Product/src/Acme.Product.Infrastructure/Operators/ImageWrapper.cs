using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Image data wrapper with ref-count lifecycle and CoW support.
/// Memory policy: keep only one strong representation (Mat or bytes) to avoid dual-cache peaks.
/// </summary>
public sealed class ImageWrapper : IDisposable
{
    private Mat? _mat;
    private int _refCount = 1;
    private readonly object _lock = new();
    private bool _disposed;
    private byte[]? _bytes;
    private readonly MatPool _pool;

    public int Width => GetMat().Width;
    public int Height => GetMat().Height;
    public int Channels => GetMat().Channels();

    public ImageWrapper(Mat mat, MatPool? pool = null)
    {
        _mat = mat ?? throw new ArgumentNullException(nameof(mat));
        _pool = pool ?? MatPool.Shared;
    }

    public ImageWrapper(byte[] bytes, MatPool? pool = null)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _pool = pool ?? MatPool.Shared;
    }

    public Mat MatReadOnly
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return GetMat();
        }
    }

    public Mat GetMat()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mat != null)
            return _mat;

        lock (_lock)
        {
            if (_mat != null)
                return _mat;

            if (_bytes == null)
                throw new InvalidOperationException("Image data is empty and cannot be decoded.");

            var decoded = Cv2.ImDecode(_bytes, ImreadModes.Color);
            if (decoded == null || decoded.Empty())
                throw new InvalidOperationException("Failed to decode image bytes.");

            _mat = decoded;

            // Switch to Mat representation and release encoded cache to avoid dual-cache memory pressure.
            _bytes = null;
            return _mat;
        }
    }

    /// <summary>
    /// CoW write access.
    /// When multiple holders exist, returns an independent pooled Mat copy.
    /// </summary>
    public Mat GetWritableMat()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var mat = GetMat();

        if (Interlocked.CompareExchange(ref _refCount, 0, 0) == 1)
            return mat;

        var pooledMat = _pool.Rent(mat.Width, mat.Height, mat.Type());
        try
        {
            mat.CopyTo(pooledMat);
            return pooledMat;
        }
        catch
        {
            _pool.Return(pooledMat);
            throw;
        }
    }

    public ImageWrapper AddRef()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Interlocked.Increment(ref _refCount);
        }

        return this;
    }

    public void Release()
    {
        int remaining = Interlocked.Decrement(ref _refCount);

        if (remaining == 0)
        {
            Dispose();
        }
        else if (remaining < 0)
        {
            Interlocked.Increment(ref _refCount);
            throw new InvalidOperationException("[ImageWrapper] RefCount became negative (double release detected).");
        }
    }

    public int RefCount => Interlocked.CompareExchange(ref _refCount, 0, 0);

    /// <summary>
    /// Returns PNG bytes by default.
    /// Cache is kept only for bytes-only wrappers; once Mat exists we avoid persisting bytes cache.
    /// </summary>
    public byte[] GetBytes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_bytes != null && _mat == null)
            return _bytes;

        lock (_lock)
        {
            if (_bytes != null && _mat == null)
                return _bytes;

            var mat = GetMat();
            return mat.ToBytes(".png");
        }
    }

    public byte[] GetBytes(string extension)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetMat().ToBytes(extension);
    }

    public byte[] ToBytes()
    {
        return GetBytes();
    }

    public bool IsDecoded => _mat != null;

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_mat != null && !_mat.IsDisposed)
                _pool.Return(_mat);

            _mat = null;
            _bytes = null;
        }

        GC.SuppressFinalize(this);
    }

    public static ImageWrapper FromBytes(byte[] bytes, MatPool? pool = null)
    {
        return new ImageWrapper(bytes, pool);
    }

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

    public static bool TryGetFromInputs(Dictionary<string, object>? inputs, string key, out ImageWrapper? image)
    {
        image = null;

        if (inputs == null)
            return false;
        if (!inputs.TryGetValue(key, out var value))
            return false;

        return TryGetFromObject(value, out image);
    }

    public static implicit operator byte[](ImageWrapper wrapper)
    {
        if (wrapper == null)
            throw new ArgumentNullException(nameof(wrapper));
        return wrapper.GetBytes();
    }

    public static (int width, int height, int channels)? TryParsePngDimensions(byte[] pngData)
    {
        if (pngData == null || pngData.Length < 24)
            return null;

        if (pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47)
            return null;

        int width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
        int height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];

        int colorType = pngData.Length > 25 ? pngData[25] : 2;
        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 3,
            4 => 2,
            6 => 4,
            _ => 3
        };

        return (width, height, channels);
    }
}
