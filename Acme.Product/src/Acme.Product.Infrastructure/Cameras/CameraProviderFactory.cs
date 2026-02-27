// CameraProviderFactory.cs
// 模拟相机提供器 - 用于测试
// 作者：蘅芜君

using System.Diagnostics;
using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 相机提供器工厂 - 支持多品牌相机发现
/// </summary>
public static class CameraProviderFactory
{
    /// <summary>
    /// 支持的制造商列表
    /// </summary>
    public static readonly string[] SupportedManufacturers = { "Huaray", "MindVision", "Hikvision" };

    /// <summary>
    /// 创建相机提供器
    /// </summary>
    public static ICameraProvider Create(string manufacturer)
    {
        return manufacturer switch
        {
            "Huaray" or "MindVision" => new MindVisionCamera(),
            "Hikvision" => new HikvisionCamera(),
            _ => throw new NotSupportedException($"不支持的相机制造商: {manufacturer}")
        };
    }

    /// <summary>
    /// 创建模拟相机提供器
    /// </summary>
    public static ICameraProvider CreateMock() => new MockCameraProvider();

    /// <summary>
    /// 发现所有品牌的相机（华睿 + 海康）
    /// </summary>
    public static List<CameraDeviceInfo> DiscoverAll()
    {
        var discoveredDevices = new List<DiscoveredDevice>();

        // 华睿相机枚举
        try
        {
            Debug.WriteLine("[CameraProviderFactory] Starting Huaray camera enumeration...");
            using var mv = new MindVisionCamera();
            var devices = mv.EnumerateDevices();
            foreach (var device in devices)
            {
                discoveredDevices.Add(new DiscoveredDevice("Huaray", device));
            }
            Debug.WriteLine($"[CameraProviderFactory] Huaray: found {devices.Count} devices");
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Huaray SDK DLL not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Huaray SDK DLL format error (32/64-bit mismatch): {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Huaray enum failed: {ex.GetType().Name} - {ex.Message}");
        }

        // 海康相机枚举
        try
        {
            Debug.WriteLine("[CameraProviderFactory] Starting Hikvision camera enumeration...");
            using var hik = new HikvisionCamera();
            var devices = hik.EnumerateDevices();
            foreach (var device in devices)
            {
                discoveredDevices.Add(new DiscoveredDevice("Hikvision", device));
            }
            Debug.WriteLine($"[CameraProviderFactory] Hikvision: found {devices.Count} devices");
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Hikvision SDK DLL not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Hikvision SDK DLL format error (32/64-bit mismatch): {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraProviderFactory] Hikvision enum failed: {ex.GetType().Name} - {ex.Message}");
        }

        var deduplicatedDevices = DeduplicateBySerial(discoveredDevices);
        Debug.WriteLine($"[CameraProviderFactory] Total cameras discovered: {discoveredDevices.Count}, deduplicated: {deduplicatedDevices.Count}");
        return deduplicatedDevices;
    }

    /// <summary>
    /// 自动检测相机
    /// </summary>
    public static ICameraProvider? AutoDetect(string serialNumber)
    {
        // 尝试华睿
        try
        {
            var mv = new MindVisionCamera();
            var devices = mv.EnumerateDevices();
            if (devices.Exists(d => d.SerialNumber == serialNumber))
            {
                return mv;
            }
            mv.Dispose();
        }
        catch (Exception ex) { Debug.WriteLine($"[CameraProviderFactory] Huaray AutoDetect failed: {ex.Message}"); }

        // 尝试海康
        try
        {
            var hik = new HikvisionCamera();
            var devices = hik.EnumerateDevices();
            if (devices.Exists(d => d.SerialNumber == serialNumber))
            {
                return hik;
            }
            hik.Dispose();
        }
        catch (Exception ex) { Debug.WriteLine($"[CameraProviderFactory] Hikvision AutoDetect failed: {ex.Message}"); }

        return null;
    }

    private static List<CameraDeviceInfo> DeduplicateBySerial(IEnumerable<DiscoveredDevice> discoveredDevices)
    {
        var serialMap = new Dictionary<string, DiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
        var unkeyedDevices = new List<CameraDeviceInfo>();

        foreach (var discovered in discoveredDevices)
        {
            var serialNumber = discovered.Device.SerialNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                unkeyedDevices.Add(discovered.Device);
                continue;
            }

            if (!serialMap.TryGetValue(serialNumber, out var existing))
            {
                serialMap[serialNumber] = discovered;
                continue;
            }

            if (ShouldReplace(existing, discovered))
            {
                serialMap[serialNumber] = discovered;
            }
        }

        var result = new List<CameraDeviceInfo>(serialMap.Count + unkeyedDevices.Count);
        foreach (var pair in serialMap)
        {
            result.Add(pair.Value.Device);
        }

        result.AddRange(unkeyedDevices);
        return result;
    }

    private static bool ShouldReplace(DiscoveredDevice existing, DiscoveredDevice candidate)
    {
        int existingMatchScore = GetSourceMatchScore(existing);
        int candidateMatchScore = GetSourceMatchScore(candidate);
        if (candidateMatchScore != existingMatchScore)
        {
            return candidateMatchScore > existingMatchScore;
        }

        int existingDetailScore = GetDetailScore(existing.Device);
        int candidateDetailScore = GetDetailScore(candidate.Device);
        if (candidateDetailScore != existingDetailScore)
        {
            return candidateDetailScore > existingDetailScore;
        }

        // Same confidence: keep existing to preserve first-found stability.
        return false;
    }

    private static int GetSourceMatchScore(DiscoveredDevice discovered)
    {
        var source = NormalizeName(discovered.SourceProvider);
        var manufacturer = NormalizeName(discovered.Device.Manufacturer);

        if (source == "huaray")
        {
            return manufacturer is "huaray" or "mindvision" ? 3 : 1;
        }

        if (source == "hikvision")
        {
            return manufacturer == "hikvision" ? 3 : 1;
        }

        return 0;
    }

    private static int GetDetailScore(CameraDeviceInfo device)
    {
        int score = 0;

        if (!string.IsNullOrWhiteSpace(device.Model) &&
            !device.Model.Equals("Huaray Camera", StringComparison.OrdinalIgnoreCase) &&
            !device.Model.StartsWith("HIK-", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (!string.IsNullOrWhiteSpace(device.UserDefinedName))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(device.InterfaceType) &&
            !device.InterfaceType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static string NormalizeName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private sealed class DiscoveredDevice
    {
        public DiscoveredDevice(string sourceProvider, CameraDeviceInfo device)
        {
            SourceProvider = sourceProvider;
            Device = device;
        }

        public string SourceProvider { get; }
        public CameraDeviceInfo Device { get; }
    }
}

/// <summary>
/// 模拟相机提供器 - 用于测试
/// </summary>
public class MockCameraProvider : ICameraProvider
{
    private bool _isConnected = false;
    private bool _isGrabbing = false;
    private bool _disposed = false;
    private byte[]? _dummyBuffer;
    private GCHandle _bufferHandle;
    private CameraDeviceInfo? _currentDevice;

    public string ProviderName => "Mock";
    public bool IsConnected => _isConnected && !_disposed;
    public bool IsGrabbing => _isGrabbing;
    public CameraDeviceInfo? CurrentDevice => _currentDevice;

    public List<CameraDeviceInfo> EnumerateDevices()
    {
        return new List<CameraDeviceInfo>
        {
            new CameraDeviceInfo
            {
                SerialNumber = "MOCK-001",
                Manufacturer = "Mock",
                Model = "Virtual Camera",
                UserDefinedName = "Mock Camera for Testing",
                InterfaceType = "Virtual"
            }
        };
    }

    public bool Open(string serialNumber)
    {
        if (_disposed) return false;

        int w = 1280, h = 1024;
        _dummyBuffer = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                _dummyBuffer[y * w + x] = (byte)((x + y) % 255);

        _bufferHandle = GCHandle.Alloc(_dummyBuffer, GCHandleType.Pinned);
        _isConnected = true;
        _currentDevice = new CameraDeviceInfo
        {
            SerialNumber = serialNumber,
            Manufacturer = "Mock",
            Model = "Virtual Camera"
        };
        return true;
    }

    public bool Close()
    {
        _isConnected = false;
        _isGrabbing = false;
        _currentDevice = null;
        if (_bufferHandle.IsAllocated)
            _bufferHandle.Free();
        _dummyBuffer = null;
        return true;
    }

    public bool StartGrabbing()
    {
        if (!_isConnected) return false;
        _isGrabbing = true;
        return true;
    }

    public bool StopGrabbing()
    {
        _isGrabbing = false;
        return true;
    }

    public CameraFrame? GetFrame(int timeoutMs = 1000)
    {
        if (!_isConnected || !_isGrabbing || _dummyBuffer == null) return null;

        Thread.Sleep(50);

        return new CameraFrame
        {
            DataPtr = _bufferHandle.AddrOfPinnedObject(),
            Width = 1280,
            Height = 1024,
            Size = _dummyBuffer.Length,
            PixelFormat = CameraPixelFormat.Mono8,
            FrameNumber = (ulong)DateTime.Now.Ticks,
            Timestamp = (ulong)DateTime.Now.Ticks,
            NeedsNativeRelease = false
        };
    }

    public bool SetExposure(double microseconds) => true;
    public bool SetGain(double value) => true;
    public bool SetTriggerMode(bool softwareTrigger) => true;
    public bool ExecuteSoftwareTrigger() => true;

    public void Dispose()
    {
        if (_disposed) return;
        Close();
        _disposed = true;
    }
}
