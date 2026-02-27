// MindVisionCamera.cs
// 华睿 (Huaray) 工业相机实现
// 作者：蘅芜君

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
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
    private static readonly object SdkLoadLock = new();
    private static Assembly? _mvSdkAssembly;
    private static string? _lastSdkLoadError;
    private static string? _lastEnumerateError;
    private bool _disposed = false;
    private bool _isConnected = false;
    private bool _isGrabbing = false;
    private CameraDeviceInfo? _currentDevice;
    private List<CameraDeviceInfo> _cachedDevices = new();

    public static bool IsSdkLoaded => _mvSdkAssembly != null;
    public static string? LastSdkLoadError => _lastSdkLoadError;
    public static string? LastEnumerateError => _lastEnumerateError;
    public static string? SdkAssemblyLocation => _mvSdkAssembly?.Location;

    public string ProviderName => "Huaray";
    public bool IsConnected => _isConnected;
    public bool IsGrabbing => _isGrabbing;
    public CameraDeviceInfo? CurrentDevice => _currentDevice;

    public MindVisionCamera()
    {
        // 延迟初始化MVSDK对象
    }

    private static bool EnsureMvSdkLoaded()
    {
        if (_mvSdkAssembly != null) return true;

        lock (SdkLoadLock)
        {
            if (_mvSdkAssembly != null) return true;

            try
            {
                _mvSdkAssembly = Assembly.Load("MVSDK_Net");
                _lastSdkLoadError = null;
                Debug.WriteLine("[MindVisionCamera] MVSDK_Net loaded via Assembly.Load");
                return true;
            }
            catch (Exception ex)
            {
                _lastSdkLoadError = ex.Message;
            }

            foreach (var candidate in GetMvSdkCandidatePaths())
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    _mvSdkAssembly = Assembly.LoadFrom(candidate);
                    _lastSdkLoadError = null;
                    Debug.WriteLine($"[MindVisionCamera] MVSDK_Net loaded from: {candidate}");
                    return true;
                }
                catch (Exception ex)
                {
                    _lastSdkLoadError = ex.Message;
                    Debug.WriteLine($"[MindVisionCamera] Failed to load MVSDK_Net from {candidate}: {ex.Message}");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(_lastSdkLoadError))
        {
            _lastSdkLoadError = "MVSDK_Net.dll not found in known locations.";
        }

        Debug.WriteLine($"[MindVisionCamera] {_lastSdkLoadError}");
        return false;
    }

    private static IEnumerable<string> GetMvSdkCandidatePaths()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                candidates.Add(path);
            }
        }

        AddPath(Path.Combine(AppContext.BaseDirectory, "MVSDK_Net.dll"));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        AddPath(Path.Combine(programFiles, "HuarayTech", "MV Viewer", "Development", "DOTNET DLL", "IMV", "DOTNET_4.0", "x64", "MVSDK_Net.dll"));
        AddPath(Path.Combine(programFiles, "HuarayTech", "MV Viewer", "Development", "DOTNET DLL", "IMV", "DOTNET_3.5", "x64", "MVSDK_Net.dll"));

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var pathEntry in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddPath(Path.Combine(pathEntry, "MVSDK_Net.dll"));
        }

        return candidates;
    }

    private static Type? GetMvSdkType(string fullTypeName)
    {
        if (!EnsureMvSdkLoaded())
        {
            return null;
        }

        return _mvSdkAssembly?.GetType(fullTypeName, throwOnError: false)
            ?? Type.GetType($"{fullTypeName}, MVSDK_Net", throwOnError: false);
    }

    public List<CameraDeviceInfo> EnumerateDevices()
    {
        _cachedDevices.Clear();
        _lastEnumerateError = null;

        try
        {
            // 使用反射调用MVSDK_Net
            var sdkType = GetMvSdkType("MVSDK_Net.MyCamera");
            if (sdkType == null)
            {
                _lastEnumerateError = LastSdkLoadError ?? "MVSDK_Net type load failed.";
                Debug.WriteLine($"[MindVisionCamera] MVSDK_Net not found: {LastSdkLoadError}");
                return _cachedDevices;
            }

            var deviceListType = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_DeviceList");
            if (deviceListType == null)
            {
                _lastEnumerateError = "MVSDK_Net.IMVDefine+IMV_DeviceList not found.";
                return _cachedDevices;
            }

            var deviceList = Activator.CreateInstance(deviceListType);
            if (deviceList == null) return _cachedDevices;

            // 调用 IMV_EnumDevices
            var enumMethod = sdkType.GetMethod("IMV_EnumDevices", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (enumMethod == null)
            {
                _lastEnumerateError = "IMV_EnumDevices method not found.";
                return _cachedDevices;
            }

            var interfaceTypeAll = GetInterfaceTypeAll();
            var args = new object[] { deviceList!, interfaceTypeAll };
            var result = enumMethod.Invoke(null, args);
            deviceList = args[0];
            if (deviceList == null)
            {
                _lastEnumerateError = "IMV_EnumDevices returned null deviceList.";
                return _cachedDevices;
            }
            var resultValue = (int)(result ?? -1);

            if (resultValue != 0) // IMV_OK = 0
            {
                _lastEnumerateError = $"IMV_EnumDevices failed: {resultValue}";
                Debug.WriteLine($"[MindVisionCamera] IMV_EnumDevices failed: {resultValue}");
                return _cachedDevices;
            }

            // 获取设备数量
            var nDevNumProp = deviceListType.GetField("nDevNum");
            var nDevNum = (uint)(nDevNumProp?.GetValue(deviceList) ?? 0u);

            if (nDevNum == 0)
            {
                var retrySummaries = new List<string>();
                var interfaceTypeEnum = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_EInterfaceType");
                if (interfaceTypeEnum != null)
                {
                    var enumNames = Enum.GetNames(interfaceTypeEnum);
                    var candidates = new[] { "interfaceTypeGige", "interfaceTypeUsb3", "interfaceTypeCL", "interfaceTypePCIe" };
                    foreach (var ifaceName in candidates)
                    {
                        if (Array.IndexOf(enumNames, ifaceName) < 0)
                        {
                            continue;
                        }

                        try
                        {
                            var ifaceValue = Convert.ToUInt32(Enum.Parse(interfaceTypeEnum, ifaceName));
                            var retryDeviceList = Activator.CreateInstance(deviceListType);
                            if (retryDeviceList == null)
                            {
                                retrySummaries.Add($"{ifaceName}:deviceList=null");
                                continue;
                            }

                            var retryArgs = new object[] { retryDeviceList, ifaceValue };
                            var retryResult = enumMethod.Invoke(null, retryArgs);
                            retryDeviceList = retryArgs[0];
                            var retryResultValue = (int)(retryResult ?? -1);
                            var retryDevNum = (uint)(nDevNumProp?.GetValue(retryDeviceList!) ?? 0u);
                            retrySummaries.Add($"{ifaceName}:res={retryResultValue},n={retryDevNum}");

                            if (retryResultValue == 0 && retryDevNum > 0)
                            {
                                deviceList = retryDeviceList;
                                nDevNum = retryDevNum;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            retrySummaries.Add($"{ifaceName}:ex={ex.Message}");
                        }
                    }
                }

                if (nDevNum == 0)
                {
                    var retryDetail = retrySummaries.Count > 0
                        ? $" Retried [{string.Join("; ", retrySummaries)}]."
                        : string.Empty;
                    _lastEnumerateError = $"IMV_EnumDevices succeeded, but nDevNum is 0.{retryDetail}";
                    return _cachedDevices;
                }
            }

            // 获取设备信息指针
            var pDevInfoProp = deviceListType.GetField("pDevInfo");
            var pDevInfo = (IntPtr)(pDevInfoProp?.GetValue(deviceList) ?? IntPtr.Zero);
            if (pDevInfo == IntPtr.Zero)
            {
                _lastEnumerateError = "IMV_EnumDevices returned nDevNum > 0 but pDevInfo is null.";
                return _cachedDevices;
            }

            var deviceInfoType = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_DeviceInfo");
            if (deviceInfoType == null)
            {
                _lastEnumerateError = "MVSDK_Net.IMVDefine+IMV_DeviceInfo not found.";
                return _cachedDevices;
            }

            var serialNumberField = deviceInfoType.GetField("serialNumber");
            var vendorNameField = deviceInfoType.GetField("vendorName");
            var modelNameField = deviceInfoType.GetField("modelName");
            var cameraNameField = deviceInfoType.GetField("cameraName");
            var interfaceNameField = deviceInfoType.GetField("interfaceName");
            var deviceInfoSize = Marshal.SizeOf(Activator.CreateInstance(deviceInfoType)!);

            for (int i = 0; i < nDevNum; i++)
            {
                var devInfoPtr = IntPtr.Add(pDevInfo, deviceInfoSize * i);
                if (devInfoPtr == IntPtr.Zero) continue;

                var devInfo = Marshal.PtrToStructure(devInfoPtr, deviceInfoType);
                if (devInfo == null) continue;

                var sn = serialNumberField?.GetValue(devInfo)?.ToString()?.Trim() ?? $"CAM_{i}";
                var vendor = vendorNameField?.GetValue(devInfo)?.ToString()?.Trim();
                var model = modelNameField?.GetValue(devInfo)?.ToString()?.Trim();
                var cameraName = cameraNameField?.GetValue(devInfo)?.ToString()?.Trim();
                var interfaceName = interfaceNameField?.GetValue(devInfo)?.ToString()?.Trim();

                _cachedDevices.Add(new CameraDeviceInfo
                {
                    SerialNumber = sn,
                    Manufacturer = string.IsNullOrWhiteSpace(vendor) ? "Huaray" : vendor,
                    Model = string.IsNullOrWhiteSpace(model) ? "Huaray Camera" : model,
                    UserDefinedName = string.IsNullOrWhiteSpace(cameraName) ? sn : cameraName,
                    InterfaceType = string.IsNullOrWhiteSpace(interfaceName) ? "Unknown" : interfaceName
                });
            }

            if (_cachedDevices.Count == 0)
            {
                _lastEnumerateError = "Device list was returned, but no valid device entries were parsed.";
            }

            Debug.WriteLine($"[MindVisionCamera] Found {_cachedDevices.Count} devices");
        }
        catch (Exception ex)
        {
            _lastEnumerateError = ex.Message;
            Debug.WriteLine($"[MindVisionCamera] EnumerateDevices error: {ex.Message}");
        }

        return _cachedDevices;
    }

    private static uint GetInterfaceTypeAll()
    {
        try
        {
            var enumType = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_EInterfaceType");
            if (enumType == null) return uint.MaxValue;

            var enumNames = Enum.GetNames(enumType);
            foreach (var enumName in enumNames)
            {
                if (enumName.Contains("all", StringComparison.OrdinalIgnoreCase))
                {
                    var value = Enum.Parse(enumType, enumName);
                    return Convert.ToUInt32(value);
                }
            }

            // 部分 SDK 版本可能没有显式 all 枚举，回退到全 bitmask。
            return uint.MaxValue;
        }
        catch
        {
            return uint.MaxValue;
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

            var sdkType = GetMvSdkType("MVSDK_Net.MyCamera");
            if (sdkType == null)
            {
                Debug.WriteLine($"[MindVisionCamera] MVSDK_Net not found: {LastSdkLoadError}");
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

            var modeType = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_ECreateHandleMode");
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
            var frameType = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_Frame");
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
            var pixelTypeEnum = GetMvSdkType("MVSDK_Net.IMVDefine+IMV_EPixelType");
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
