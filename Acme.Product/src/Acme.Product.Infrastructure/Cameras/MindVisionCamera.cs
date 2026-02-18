// MindVisionCamera.cs
// 华睿 (Huaray) 工业相机实现
// 作者：蘅芜君

using System.Diagnostics;
using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 华睿 (Huaray) 工业相机实现
/// 注意：类名保留 MindVisionCamera 以兼容历史代码
/// </summary>
public class MindVisionCamera : ICameraProvider
{
    // MVSDK_Net 类型引用 - 使用动态加载避免编译依赖
    private object? _cam;
    private bool _disposed = false;
    private bool _isConnected = false;
    private bool _isGrabbing = false;
    private CameraDeviceInfo? _currentDevice;
    private List<CameraDeviceInfo> _cachedDevices = new();

    public string ProviderName => "Huaray";
    public bool IsConnected => _isConnected;
    public bool IsGrabbing => _isGrabbing;
    public CameraDeviceInfo? CurrentDevice => _currentDevice;

    public MindVisionCamera()
    {
        // 延迟初始化MVSDK对象
    }

    public List<CameraDeviceInfo> EnumerateDevices()
    {
        _cachedDevices.Clear();

        try
        {
            // 使用反射调用MVSDK_Net
            var sdkType = Type.GetType("MVSDK_Net.MyCamera, MVSDK_Net");
            if (sdkType == null)
            {
                Debug.WriteLine("[MindVisionCamera] MVSDK_Net not found");
                return _cachedDevices;
            }

            var deviceListType = Type.GetType("MVSDK_Net.IMVDefine+IMV_DeviceList, MVSDK_Net");
            if (deviceListType == null) return _cachedDevices;

            var deviceList = Activator.CreateInstance(deviceListType);
            if (deviceList == null) return _cachedDevices;

            // 调用 IMV_EnumDevices
            var enumMethod = sdkType.GetMethod("IMV_EnumDevices", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (enumMethod == null) return _cachedDevices;

            var interfaceTypeAll = GetInterfaceTypeAll();
            var result = enumMethod.Invoke(null, new object[] { deviceList!, interfaceTypeAll });
            var resultValue = (int)(result ?? -1);

            if (resultValue != 0) // IMV_OK = 0
            {
                Debug.WriteLine($"[MindVisionCamera] IMV_EnumDevices failed: {resultValue}");
                return _cachedDevices;
            }

            // 获取设备数量
            var nDevNumProp = deviceListType.GetField("nDevNum");
            var nDevNum = (uint)(nDevNumProp?.GetValue(deviceList) ?? 0u);

            if (nDevNum == 0) return _cachedDevices;

            // 获取设备信息指针
            var pDevInfoProp = deviceListType.GetField("pDevInfo");
            var pDevInfo = pDevInfoProp?.GetValue(deviceList);

            if (pDevInfo is not IntPtr[] devicePtrs) return _cachedDevices;

            var deviceInfoType = Type.GetType("MVSDK_Net.IMVDefine+IMV_DeviceInfo, MVSDK_Net");
            if (deviceInfoType == null) return _cachedDevices;

            var serialNumberField = deviceInfoType.GetField("serialNumber");

            for (int i = 0; i < nDevNum; i++)
            {
                if (devicePtrs[i] == IntPtr.Zero) continue;

                var devInfo = Marshal.PtrToStructure(devicePtrs[i], deviceInfoType);
                if (devInfo == null) continue;

                var sn = serialNumberField?.GetValue(devInfo)?.ToString()?.Trim() ?? $"CAM_{i}";

                _cachedDevices.Add(new CameraDeviceInfo
                {
                    SerialNumber = sn,
                    Manufacturer = "Huaray",
                    Model = "Huaray Camera",
                    UserDefinedName = sn,
                    InterfaceType = "Unknown"
                });
            }

            Debug.WriteLine($"[MindVisionCamera] Found {_cachedDevices.Count} devices");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] EnumerateDevices error: {ex.Message}");
        }

        return _cachedDevices;
    }

    private static uint GetInterfaceTypeAll()
    {
        try
        {
            var enumType = Type.GetType("MVSDK_Net.IMVDefine+IMV_EInterfaceType, MVSDK_Net");
            if (enumType == null) return 0;

            var value = Enum.Parse(enumType, "interfaceTypeAll");
            return (uint)(int)value;
        }
        catch
        {
            return 0;
        }
    }

    public bool Open(string serialNumber)
    {
        if (IsConnected) Close();

        try
        {
            if (_cachedDevices.Count == 0)
                EnumerateDevices();

            int deviceIndex = -1;
            for (int i = 0; i < _cachedDevices.Count; i++)
            {
                if (_cachedDevices[i].SerialNumber == serialNumber)
                {
                    deviceIndex = i;
                    break;
                }
            }

            if (deviceIndex < 0)
            {
                Debug.WriteLine($"[MindVisionCamera] Device not found: {serialNumber}");
                return false;
            }

            var sdkType = Type.GetType("MVSDK_Net.MyCamera, MVSDK_Net");
            if (sdkType == null)
            {
                Debug.WriteLine("[MindVisionCamera] MVSDK_Net not found");
                return false;
            }

            _cam = Activator.CreateInstance(sdkType);
            if (_cam == null)
            {
                Debug.WriteLine("[MindVisionCamera] Failed to create camera instance");
                return false;
            }

            // 创建句柄
            var createHandleMethod = sdkType.GetMethod("IMV_CreateHandle");
            if (createHandleMethod == null)
            {
                Debug.WriteLine("[MindVisionCamera] IMV_CreateHandle not found");
                return false;
            }

            var modeType = Type.GetType("MVSDK_Net.IMVDefine+IMV_ECreateHandleMode, MVSDK_Net");
            var modeByIndex = modeType != null ? Enum.Parse(modeType, "modeByIndex") : 0;

            var handleResult = createHandleMethod.Invoke(_cam, new object[] { modeByIndex, deviceIndex });
            if ((int)(handleResult ?? -1) != 0)
            {
                Debug.WriteLine("[MindVisionCamera] CreateHandle failed");
                _cam = null;
                return false;
            }

            // 打开设备
            var openMethod = sdkType.GetMethod("IMV_Open");
            if (openMethod == null)
            {
                Debug.WriteLine("[MindVisionCamera] IMV_Open not found");
                _cam = null;
                return false;
            }

            var openResult = openMethod.Invoke(_cam, null);
            if ((int)(openResult ?? -1) != 0)
            {
                Debug.WriteLine("[MindVisionCamera] Open failed");
                DestroyHandle();
                _cam = null;
                return false;
            }

            _isConnected = true;
            _currentDevice = _cachedDevices[deviceIndex];
            Debug.WriteLine($"[MindVisionCamera] Opened: {serialNumber}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MindVisionCamera] Open error: {ex.Message}");
            return false;
        }
    }

    private void DestroyHandle()
    {
        if (_cam == null) return;

        try
        {
            var sdkType = _cam.GetType();
            var destroyMethod = sdkType.GetMethod("IMV_DestroyHandle");
            destroyMethod?.Invoke(_cam, null);
        }
        catch { }
    }

    public bool Close()
    {
        if (!_isConnected) return true;

        try
        {
            if (_isGrabbing)
                StopGrabbing();

            if (_cam != null)
            {
                var sdkType = _cam.GetType();

                var closeMethod = sdkType.GetMethod("IMV_Close");
                closeMethod?.Invoke(_cam, null);

                DestroyHandle();

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
        if (!_isConnected) return false;
        if (_isGrabbing) return true;
        if (_cam == null) return false;

        try
        {
            var sdkType = _cam.GetType();
            var startMethod = sdkType.GetMethod("IMV_StartGrabbing");
            if (startMethod == null) return false;

            var result = startMethod.Invoke(_cam, null);
            _isGrabbing = (int)(result ?? -1) == 0;

            Debug.WriteLine($"[MindVisionCamera] StartGrabbing: {(_isGrabbing ? "OK" : "Failed")}");
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
        if (!_isConnected) return true;
        if (!_isGrabbing) return true;
        if (_cam == null) return true;

        try
        {
            var sdkType = _cam.GetType();
            var stopMethod = sdkType.GetMethod("IMV_StopGrabbing");
            if (stopMethod != null)
            {
                stopMethod.Invoke(_cam, null);
            }
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
        if (!_isConnected || !_isGrabbing || _cam == null) return null;

        try
        {
            var sdkType = _cam.GetType();

            // 获取IMV_Frame类型
            var frameType = Type.GetType("MVSDK_Net.IMVDefine+IMV_Frame, MVSDK_Net");
            if (frameType == null) return null;

            var frame = Activator.CreateInstance(frameType);
            if (frame == null) return null;

            var getFrameMethod = sdkType.GetMethod("IMV_GetFrame");
            if (getFrameMethod == null) return null;

            var result = getFrameMethod.Invoke(_cam, new object[] { frame!, (uint)timeoutMs });
            if ((int)(result ?? -1) != 0) return null;

            // 读取帧信息
            var frameInfoField = frameType.GetField("frameInfo");
            var pDataField = frameType.GetField("pData");

            var frameInfo = frameInfoField?.GetValue(frame);
            var pData = pDataField?.GetValue(frame);

            if (frameInfo == null) return null;

            var frameInfoType = frameInfo.GetType();
            var widthField = frameInfoType.GetField("width");
            var heightField = frameInfoType.GetField("height");
            var sizeField = frameInfoType.GetField("size");
            var pixelFormatField = frameInfoType.GetField("pixelFormat");

            var width = (uint)(widthField?.GetValue(frameInfo) ?? 0u);
            var height = (uint)(heightField?.GetValue(frameInfo) ?? 0u);
            var size = (uint)(sizeField?.GetValue(frameInfo) ?? 0u);
            var pixelFormat = (uint)(pixelFormatField?.GetValue(frameInfo) ?? 0u);

            return new CameraFrame
            {
                DataPtr = (IntPtr)(pData ?? IntPtr.Zero),
                Width = (int)width,
                Height = (int)height,
                Size = (int)size,
                PixelFormat = ConvertPixelFormat(pixelFormat),
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

    private static CameraPixelFormat ConvertPixelFormat(uint pixelType)
    {
        try
        {
            var pixelTypeEnum = Type.GetType("MVSDK_Net.IMVDefine+IMV_EPixelType, MVSDK_Net");
            if (pixelTypeEnum == null) return CameraPixelFormat.Unknown;

            var mono8Value = Enum.Parse(pixelTypeEnum, "gvspPixelMono8");
            var mono8Int = (int)mono8Value;

            return pixelType == mono8Int ? CameraPixelFormat.Mono8 : CameraPixelFormat.Unknown;
        }
        catch
        {
            return CameraPixelFormat.Unknown;
        }
    }

    public bool SetExposure(double microseconds)
    {
        if (!_isConnected || _cam == null) return false;

        try
        {
            var sdkType = _cam.GetType();
            var setMethod = sdkType.GetMethod("IMV_SetDoubleFeatureValue");
            if (setMethod == null) return false;

            var result = setMethod.Invoke(_cam, new object[] { "ExposureTime", microseconds });
            return (int)(result ?? -1) == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool SetGain(double value)
    {
        if (!_isConnected || _cam == null) return false;

        try
        {
            var sdkType = _cam.GetType();
            var setMethod = sdkType.GetMethod("IMV_SetDoubleFeatureValue");
            if (setMethod == null) return false;

            var result = setMethod.Invoke(_cam, new object[] { "GainRaw", value });
            return (int)(result ?? -1) == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool SetTriggerMode(bool softwareTrigger)
    {
        if (!_isConnected || _cam == null) return false;

        try
        {
            var sdkType = _cam.GetType();
            var setEnumMethod = sdkType.GetMethod("IMV_SetEnumFeatureSymbol");
            if (setEnumMethod == null) return false;

            if (softwareTrigger)
            {
                setEnumMethod.Invoke(_cam, new object[] { "TriggerMode", "On" });
                var result = setEnumMethod.Invoke(_cam, new object[] { "TriggerSource", "Software" });
                return (int)(result ?? -1) == 0;
            }
            else
            {
                var result = setEnumMethod.Invoke(_cam, new object[] { "TriggerMode", "Off" });
                return (int)(result ?? -1) == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    public bool ExecuteSoftwareTrigger()
    {
        if (!_isConnected || _cam == null) return false;

        try
        {
            var sdkType = _cam.GetType();
            var execMethod = sdkType.GetMethod("IMV_ExecuteCommandFeature");
            if (execMethod == null) return false;

            var result = execMethod.Invoke(_cam, new object[] { "TriggerSoftware" });
            return (int)(result ?? -1) == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        try
        {
            Close();
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
