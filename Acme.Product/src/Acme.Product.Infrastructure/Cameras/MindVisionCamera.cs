// MindVisionCamera.cs
// 华睿 (Huaray) 工业相机实现 — 直接引用 MVSDK_Net
// 作者：蘅芜君

using System.Diagnostics;
using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;
using MVSDK_Net;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 华睿 (Huaray) 工业相机实现
/// 注意：类名保留 MindVisionCamera 以兼容历史代码
/// </summary>
public class MindVisionCamera : ICameraProvider
{
    private MyCamera? _cam;
    private bool _disposed = false;
    private bool _isConnected = false;
    private bool _isGrabbing = false;
    private CameraDeviceInfo? _currentDevice;
    private List<CameraDeviceInfo> _cachedDevices = new();

    // 最近一帧引用（用于释放）
    private IMVDefine.IMV_Frame _lastFrame;
    private bool _hasUnreleasedFrame = false;

    // 原生 DLL 搜索路径设置
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    // 静态标记：确保 DLL 路径只设置一次
    private static bool _nativePathConfigured = false;

    public string ProviderName => "Huaray";
    public bool IsConnected => _isConnected;
    public bool IsGrabbing => _isGrabbing;
    public CameraDeviceInfo? CurrentDevice => _currentDevice;

    // 兼容性属性 —— 供 SettingsEndpoints 诊断接口使用
    public static bool IsSdkLoaded => true;
    public static string? LastSdkLoadError => null;
    public static string? LastEnumerateError { get; private set; }
    public static string? SdkAssemblyLocation => AppContext.BaseDirectory;

    public MindVisionCamera()
    {
        EnsureNativeDllPath();
    }

    /// <summary>
    /// 确保原生 SDK DLL（MVSDKmd.dll 等）的搜索路径已设置。
    /// 必须在任何 MVSDK_Net 类型被使用之前调用。
    /// </summary>
    private static void EnsureNativeDllPath()
    {
        if (_nativePathConfigured)
            return;

        try
        {
            // 优先使用应用程序目录（编译时已将原生 DLL 复制到此处）
            SetDllDirectory(AppContext.BaseDirectory);
            Debug.WriteLine($"[MindVisionCamera] SetDllDirectory: {AppContext.BaseDirectory}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] SetDllDirectory failed: {ex.Message}");
        }

        _nativePathConfigured = true;
    }

    public List<CameraDeviceInfo> EnumerateDevices()
    {
        _cachedDevices.Clear();
        LastEnumerateError = null;

        try
        {
            var deviceList = new IMVDefine.IMV_DeviceList();
            int res = MyCamera.IMV_EnumDevices(ref deviceList, (uint)IMVDefine.IMV_EInterfaceType.interfaceTypeAll);

            if (res != IMVDefine.IMV_OK)
            {
                LastEnumerateError = $"IMV_EnumDevices failed: {res}";
                Debug.WriteLine($"[MindVisionCamera] IMV_EnumDevices failed: {res}");
                return _cachedDevices;
            }

            if (deviceList.nDevNum == 0)
            {
                LastEnumerateError = "IMV_EnumDevices succeeded, but nDevNum is 0.";
                Debug.WriteLine("[MindVisionCamera] IMV_EnumDevices: 0 devices");
                return _cachedDevices;
            }

            int structSize = Marshal.SizeOf(typeof(IMVDefine.IMV_DeviceInfo));

            for (int i = 0; i < (int)deviceList.nDevNum; i++)
            {
                var devInfoPtr = deviceList.pDevInfo + structSize * i;
                if (devInfoPtr == IntPtr.Zero)
                    continue;

                var devInfo = (IMVDefine.IMV_DeviceInfo)Marshal.PtrToStructure(
                    devInfoPtr, typeof(IMVDefine.IMV_DeviceInfo))!;

                var sn = (devInfo.serialNumber ?? $"CAM_{i}").Trim();
                var vendor = (devInfo.vendorName ?? string.Empty).Trim();
                var model = (devInfo.modelName ?? string.Empty).Trim();
                var cameraName = (devInfo.cameraName ?? string.Empty).Trim();
                var interfaceName = (devInfo.interfaceName ?? string.Empty).Trim();

                _cachedDevices.Add(new CameraDeviceInfo
                {
                    SerialNumber = sn,
                    Manufacturer = string.IsNullOrWhiteSpace(vendor) ? "Huaray" : vendor,
                    Model = string.IsNullOrWhiteSpace(model) ? "Huaray Camera" : model,
                    UserDefinedName = string.IsNullOrWhiteSpace(cameraName) ? sn : cameraName,
                    IpAddress = TryReadIpAddress(devInfo),
                    InterfaceType = string.IsNullOrWhiteSpace(interfaceName) ? "Unknown" : interfaceName
                });
            }

            Debug.WriteLine($"[MindVisionCamera] Found {_cachedDevices.Count} devices");
        }
        catch (Exception ex)
        {
            LastEnumerateError = ex.Message;
            Debug.WriteLine($"[MindVisionCamera] EnumerateDevices error: {ex.Message}");
        }

        return _cachedDevices;
    }

    private static string? TryReadIpAddress(IMVDefine.IMV_DeviceInfo devInfo)
    {
        foreach (var memberName in new[] { "ipAddress", "IpAddress", "cameraIp", "CameraIp", "deviceIp", "DeviceIp", "currentIp", "CurrentIp" })
        {
            var value = TryReadMemberValue(devInfo, memberName);
            var normalized = NormalizeIpString(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static object? TryReadMemberValue<T>(T target, string memberName)
    {
        var type = target?.GetType();
        if (type == null)
        {
            return null;
        }

        var field = type.GetField(memberName);
        if (field != null)
        {
            return field.GetValue(target);
        }

        var property = type.GetProperty(memberName);
        return property?.GetValue(target);
    }

    private static string? NormalizeIpString(object? rawValue)
    {
        if (rawValue == null)
        {
            return null;
        }

        var text = rawValue.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) ||
            text == "0.0.0.0" ||
            text == "255.255.255.255")
        {
            return null;
        }

        return text;
    }

    public bool Open(string serialNumber)
    {
        // 打开前无条件执行完整资源释放，避免残留句柄导致失败
        ForceRelease();

        try
        {
            // 每次 Open 前强制重新枚举，确保 SDK 全局设备列表与物理状态一致
            EnumerateDevices();

            // 按序列号找设备索引
            string target = (serialNumber ?? string.Empty).Trim();
            int deviceIndex = -1;
            for (int i = 0; i < _cachedDevices.Count; i++)
            {
                if (string.Equals(_cachedDevices[i].SerialNumber?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    deviceIndex = i;
                    break;
                }
            }

            if (deviceIndex < 0)
            {
                var knownSerials = string.Join(", ", _cachedDevices.Select(d => d.SerialNumber));
                throw new InvalidOperationException(
                    $"SDK 枚举到 {_cachedDevices.Count} 台设备 [{knownSerials}]，但未找到序列号 '{target}'。" +
                    "请检查相机供电、网线连接及网段配置。");
            }

            // 创建实例
            _cam = new MyCamera();

            // 建句柄
            int res = _cam.IMV_CreateHandle(IMVDefine.IMV_ECreateHandleMode.modeByIndex, deviceIndex);
            if (res != IMVDefine.IMV_OK)
            {
                _cam = null;
                throw new InvalidOperationException(
                    $"IMV_CreateHandle 失败 (错误码={res}, deviceIndex={deviceIndex}, SN={target})。" +
                    "可能原因：设备已被其他进程占用，或 SDK 内部状态异常。");
            }

            // 打开设备
            res = _cam.IMV_Open();
            if (res != IMVDefine.IMV_OK)
            {
                _cam.IMV_DestroyHandle();
                _cam = null;
                throw new InvalidOperationException(
                    $"IMV_Open 失败 (错误码={res}, SN={target})。" +
                    "可能原因：相机已被其他软件占用、网络不可达、或 IP 子网不匹配。");
            }

            _isConnected = true;
            _currentDevice = _cachedDevices[deviceIndex];
            Debug.WriteLine($"[MindVisionCamera] Opened: {serialNumber}");
            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] Open error: {ex.Message}");
            throw new InvalidOperationException($"打开相机时异常: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 无条件释放所有 SDK 资源（StopGrabbing → Close → DestroyHandle）
    /// </summary>
    private void ForceRelease()
    {
        try
        {
            ReleaseLastFrame();

            if (_cam != null)
            {
                if (_isGrabbing)
                {
                    try
                    { _cam.IMV_StopGrabbing(); }
                    catch { }
                    _isGrabbing = false;
                }

                try
                { _cam.IMV_Close(); }
                catch { }
                try
                { _cam.IMV_DestroyHandle(); }
                catch { }
                _cam = null;
            }

            _isConnected = false;
            _currentDevice = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] ForceRelease error: {ex.Message}");
        }
    }

    public bool Close()
    {
        if (!_isConnected)
            return true;

        try
        {
            ReleaseLastFrame();

            if (_isGrabbing)
                StopGrabbing();

            if (_cam != null)
            {
                _cam.IMV_Close();
                _cam.IMV_DestroyHandle();
                _cam = null;
            }

            _isConnected = false;
            _currentDevice = null;
            Debug.WriteLine("[MindVisionCamera] Closed");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] Close error: {ex.Message}");
            return false;
        }
    }

    public bool StartGrabbing()
    {
        if (!_isConnected)
            return false;
        if (_isGrabbing)
            return true;
        if (_cam == null)
            return false;

        try
        {
            int res = _cam.IMV_StartGrabbing();
            _isGrabbing = res == IMVDefine.IMV_OK;

            Debug.WriteLine($"[MindVisionCamera] StartGrabbing: {(_isGrabbing ? "OK" : $"Failed({res})")}");
            return _isGrabbing;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] StartGrabbing error: {ex.Message}");
            return false;
        }
    }

    public bool StopGrabbing()
    {
        if (!_isConnected)
            return true;
        if (!_isGrabbing)
            return true;
        if (_cam == null)
            return true;

        try
        {
            _cam.IMV_StopGrabbing();
            _isGrabbing = false;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] StopGrabbing error: {ex.Message}");
            return false;
        }
    }

    public CameraFrame? GetFrame(int timeoutMs = 1000)
    {
        if (!_isConnected || !_isGrabbing || _cam == null)
            return null;

        try
        {
            // 释放上一帧
            ReleaseLastFrame();

            var frame = new IMVDefine.IMV_Frame();
            int res = _cam.IMV_GetFrame(ref frame, (uint)timeoutMs);
            if (res != IMVDefine.IMV_OK)
                return null;

            // 保存帧引用以供后续释放
            _lastFrame = frame;
            _hasUnreleasedFrame = true;

            var width = frame.frameInfo.width;
            var height = frame.frameInfo.height;
            var size = frame.frameInfo.size;
            var pixelFormat = frame.frameInfo.pixelFormat;

            return new CameraFrame
            {
                DataPtr = frame.pData,
                Width = (int)width,
                Height = (int)height,
                Size = (int)size,
                PixelFormat = ConvertPixelFormat((uint)pixelFormat),
                FrameNumber = 0,
                Timestamp = 0,
                NeedsNativeRelease = true
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] GetFrame error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 释放上一次 GetFrame 获取的帧资源
    /// </summary>
    private void ReleaseLastFrame()
    {
        if (!_hasUnreleasedFrame || _cam == null)
            return;

        try
        {
            _cam.IMV_ReleaseFrame(ref _lastFrame);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] ReleaseFrame error: {ex.Message}");
        }
        finally
        {
            _hasUnreleasedFrame = false;
        }
    }

    private static CameraPixelFormat ConvertPixelFormat(uint pixelType)
    {
        // gvspPixelMono8 = 0x01080001
        return pixelType == 0x01080001 ? CameraPixelFormat.Mono8 : CameraPixelFormat.Unknown;
    }

    public bool SetExposure(double microseconds)
    {
        if (!_isConnected || _cam == null)
            return false;

        try
        {
            int res = _cam.IMV_SetDoubleFeatureValue("ExposureTime", microseconds);
            return res == IMVDefine.IMV_OK;
        }
        catch { return false; }
    }

    public bool SetGain(double value)
    {
        if (!_isConnected || _cam == null)
            return false;

        try
        {
            int res = _cam.IMV_SetDoubleFeatureValue("GainRaw", value);
            return res == IMVDefine.IMV_OK;
        }
        catch { return false; }
    }

    public bool SetTriggerMode(bool softwareTrigger)
    {
        if (!_isConnected || _cam == null)
            return false;

        try
        {
            if (softwareTrigger)
            {
                _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "On");
                int res = _cam.IMV_SetEnumFeatureSymbol("TriggerSource", "Software");
                return res == IMVDefine.IMV_OK;
            }
            else
            {
                int res = _cam.IMV_SetEnumFeatureSymbol("TriggerMode", "Off");
                return res == IMVDefine.IMV_OK;
            }
        }
        catch { return false; }
    }

    public bool ExecuteSoftwareTrigger()
    {
        if (!_isConnected || _cam == null)
            return false;

        try
        {
            int res = _cam.IMV_ExecuteCommandFeature("TriggerSoftware");
            return res == IMVDefine.IMV_OK;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            ForceRelease();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] Dispose error: {ex.Message}");
        }
        finally
        {
            _disposed = true;
        }
    }

    ~MindVisionCamera() => Dispose(false);
}
