// CameraManager.cs
// 相机管理器实�?- 支持硬件绑定与逻辑ID映射
// 作者：蘅芜�?
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 相机管理器实�?/// </summary>
public class CameraManager : ICameraManager, IDisposable
{
    private readonly ConcurrentDictionary<string, ICamera> _cameras = new();
    private readonly ConcurrentDictionary<string, ICameraProvider> _providers = new();
    private List<CameraBindingConfig> _bindings = new();
    private string _activeCameraId = "";
    private bool _disposed;

    /// <summary>
    /// 枚举所有可用相机设�?    /// </summary>
    public Task<IEnumerable<CameraInfo>> EnumerateCamerasAsync()
    {
        var allDevices = CameraProviderFactory.DiscoverAll();
        var cameraInfos = allDevices.Select(d => new CameraInfo
        {
            CameraId = d.SerialNumber,
            Name = string.IsNullOrEmpty(d.UserDefinedName) ? d.Model : d.UserDefinedName,
            Manufacturer = d.Manufacturer,
            Model = d.Model,
            ConnectionType = d.InterfaceType,
            IsConnected = _cameras.ContainsKey(d.SerialNumber)
        });

        return Task.FromResult(cameraInfos);
    }

    /// <summary>
    /// 获取或创建相机（基于原始序列号）
    /// </summary>
    public Task<ICamera> GetOrCreateCameraAsync(string cameraId)
    {
        if (_cameras.TryGetValue(cameraId, out var existingCamera))
        {
            return Task.FromResult(existingCamera);
        }

        // AutoDetect internally opens the camera and returns a connected provider.
        var provider = CameraProviderFactory.AutoDetect(cameraId);
        if (provider == null)
            throw new InvalidOperationException($"Failed to detect camera: {cameraId}. Check power, connection, and SDK installation.");

        var cameraAdapter = new CameraProviderAdapter(cameraId, provider);
        _cameras[cameraId] = cameraAdapter;
        _providers[cameraId] = provider;

        return Task.FromResult<ICamera>(cameraAdapter);
    }

    /// <summary>
    /// 根据绑定ID获取相机
    /// </summary>
    public async Task<ICamera> GetOrCreateByBindingAsync(string bindingId)
    {
        var binding = _bindings.FirstOrDefault(b => b.Id == bindingId);
        if (binding == null)
        {
            // 如果找不到绑定，尝试直接作为SN处理（向下兼容）
            return await GetOrCreateCameraAsync(bindingId);
        }

        if (string.IsNullOrEmpty(binding.SerialNumber))
        {
            throw new InvalidOperationException($"绑定 '{binding.DisplayName}' 未关联物理设备序列号");
        }

        return await GetOrCreateCameraAsync(binding.SerialNumber);
    }

    public Task<ICamera> OpenCameraAsync(string cameraId) => GetOrCreateCameraAsync(cameraId);

    public Task CloseCameraAsync(string cameraId)
    {
        if (_cameras.TryRemove(cameraId, out var camera))
            camera.Dispose();
        if (_providers.TryRemove(cameraId, out var provider))
            provider.Dispose();
        return Task.CompletedTask;
    }

    public ICamera? GetCamera(string cameraId)
    {
        _cameras.TryGetValue(cameraId, out var camera);
        return camera;
    }

    public Task DisconnectAllAsync()
    {
        foreach (var camera in _cameras.Values)
            camera.Dispose();
        _cameras.Clear();
        _providers.Clear();
        return Task.CompletedTask;
    }

    // --- 相机绑定管理功能 ---

    public void LoadBindings(List<CameraBindingConfig> bindings, string activeCameraId)
    {
        _bindings = bindings ?? new List<CameraBindingConfig>();
        _activeCameraId = activeCameraId ?? "";
        Debug.WriteLine($"[CameraManager] Loaded {_bindings.Count} camera bindings");
    }

    public List<CameraBindingConfig> GetBindings() => _bindings;

    public void UpdateBindings(List<CameraBindingConfig> bindings, string activeCameraId)
    {
        _bindings = bindings ?? new List<CameraBindingConfig>();
        _activeCameraId = activeCameraId ?? "";
        Debug.WriteLine($"[CameraManager] Updated bindings, active camera: {_activeCameraId}");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisconnectAllAsync().Wait();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 相机提供器适配�?/// </summary>
public class CameraProviderAdapter : IIndustrialCamera
{
    private readonly string _cameraId;
    private readonly ICameraProvider _provider;
    private bool _isAcquiring;
    private Func<byte[], Task>? _frameCallback;
    private CancellationTokenSource? _acquisitionCts;
    private Task? _acquisitionTask;

    public string CameraId => _cameraId;
    public string Name => _provider.CurrentDevice?.UserDefinedName ?? _cameraId;
    public bool IsConnected => _provider.IsConnected;
    public bool IsAcquiring => _isAcquiring;

    public event EventHandler<CameraFrameReceivedEventArgs>? FrameReceived;

    public CameraProviderAdapter(string cameraId, ICameraProvider provider)
    {
        _cameraId = cameraId;
        _provider = provider;
    }

    public Task ConnectAsync() => Task.CompletedTask;
    public Task DisconnectAsync() { _provider.Close(); return Task.CompletedTask; }

    public async Task<byte[]> AcquireSingleFrameAsync()
    {
        // 严格按照华睿 SDK 正确时序�?
        // 1) StartGrabbing -> 2) TriggerMode=On,TriggerSource=Software -> 3) ExecuteSoftwareTrigger -> 4) GetFrame

        // 1) 确保采集已启�?
        if (!_provider.IsGrabbing)
            _provider.StartGrabbing();

        // 2) 设置软件触发模式（TriggerMode=On, TriggerSource=Software�?
        _provider.SetTriggerMode(true);

        // 3) 发送软触发命令
        _provider.ExecuteSoftwareTrigger();

        // 4) 获取帧（�?SDK 足够响应时间�?
        var frame = _provider.GetFrame(3000);
        if (frame == null)
            throw new TimeoutException("获取图像超时");

        byte[] pngData = EncodeFrameToBytes(frame);
        return await Task.FromResult(pngData);
    }

    public Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback)
    {
        if (_isAcquiring)
            return Task.CompletedTask;

        _frameCallback = frameCallback;
        _isAcquiring = true;

        _acquisitionCts?.Dispose();
        _acquisitionCts = new CancellationTokenSource();
        var token = _acquisitionCts.Token;

        _acquisitionTask = Task.Run(async () =>
        {
            try
            {
                if (!_provider.IsGrabbing)
                    _provider.StartGrabbing();

                while (!token.IsCancellationRequested)
                {
                    var frame = _provider.GetFrame(1000);
                    if (frame == null)
                        continue;

                    byte[] imageData = EncodeFrameToBytes(frame, useFastEncoding: true);
                    if (_frameCallback != null)
                        await _frameCallback(imageData);

                    FrameReceived?.Invoke(this, new CameraFrameReceivedEventArgs { ImageData = imageData, Width = frame.Width, Height = frame.Height });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraProviderAdapter] Continuous acquisition error: {ex.Message}");
            }
        }, token);

        return Task.CompletedTask;
    }

    public async Task StopContinuousAcquisitionAsync()
    {
        _isAcquiring = false;

        if (_acquisitionCts != null)
        {
            _acquisitionCts.Cancel();
        }

        if (_acquisitionTask != null)
        {
            try
            {
                await _acquisitionTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraProviderAdapter] Stop acquisition wait error: {ex.Message}");
            }
            finally
            {
                _acquisitionTask = null;
            }
        }

        _acquisitionCts?.Dispose();
        _acquisitionCts = null;
        _provider.StopGrabbing();
    }

    public Task SetExposureTimeAsync(double exposureTime) { _provider.SetExposure(exposureTime); return Task.CompletedTask; }
    public Task SetGainAsync(double gain) { _provider.SetGain(gain); return Task.CompletedTask; }
    public Task SetTriggerModeAsync(bool isHardwareTrigger) { _provider.SetTriggerMode(!isHardwareTrigger); return Task.CompletedTask; }
    public Task ExecuteSoftwareTriggerAsync() { _provider.ExecuteSoftwareTrigger(); return Task.CompletedTask; }

    public CameraParameters GetParameters() => new CameraParameters();

    public void Dispose()
    {
        StopContinuousAcquisitionAsync().GetAwaiter().GetResult();
        _provider.Dispose();
    }

    private byte[] EncodeFrameToBytes(CameraFrame frame, bool useFastEncoding = false)
    {
        OpenCvSharp.MatType matType;
        bool needConversion = false;
        OpenCvSharp.ColorConversionCodes conversionCode = OpenCvSharp.ColorConversionCodes.BayerBG2BGR;

        switch (frame.PixelFormat)
        {
            case CameraPixelFormat.Mono8:
                matType = OpenCvSharp.MatType.CV_8UC1;
                break;
            case CameraPixelFormat.RGB8:
                matType = OpenCvSharp.MatType.CV_8UC3;
                needConversion = true;
                conversionCode = OpenCvSharp.ColorConversionCodes.RGB2BGR;
                break;
            case CameraPixelFormat.BGR8:
                matType = OpenCvSharp.MatType.CV_8UC3;
                break;
            case CameraPixelFormat.BayerRG8:
                matType = OpenCvSharp.MatType.CV_8UC1;
                needConversion = true;
                conversionCode = OpenCvSharp.ColorConversionCodes.BayerRG2BGR;
                break;
            case CameraPixelFormat.BayerGB8:
                matType = OpenCvSharp.MatType.CV_8UC1;
                needConversion = true;
                conversionCode = OpenCvSharp.ColorConversionCodes.BayerGB2BGR;
                break;
            case CameraPixelFormat.BayerGR8:
                matType = OpenCvSharp.MatType.CV_8UC1;
                needConversion = true;
                conversionCode = OpenCvSharp.ColorConversionCodes.BayerGR2BGR;
                break;
            case CameraPixelFormat.BayerBG8:
                matType = OpenCvSharp.MatType.CV_8UC1;
                needConversion = true;
                conversionCode = OpenCvSharp.ColorConversionCodes.BayerBG2BGR;
                break;
            default:
                matType = OpenCvSharp.MatType.CV_8UC1;
                break;
        }

        using var mat = new OpenCvSharp.Mat(frame.Height, frame.Width, matType, frame.DataPtr);

        if (needConversion)
        {
            using var cvtMat = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(mat, cvtMat, conversionCode);
            if (useFastEncoding)
                return cvtMat.ToBytes(".jpg", new int[] { (int)OpenCvSharp.ImwriteFlags.JpegQuality, 85 });
            return cvtMat.ToBytes(".png");
        }
        else
        {
            if (useFastEncoding)
                return mat.ToBytes(".jpg", new int[] { (int)OpenCvSharp.ImwriteFlags.JpegQuality, 85 });
            return mat.ToBytes(".png");
        }
    }
}

