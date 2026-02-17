using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 相机管理器 - 统一管理多个相机设备
/// </summary>
public class CameraManager : ICameraManager, IDisposable
{
    private readonly ConcurrentDictionary<string, ICamera> _cameras = new();
    private readonly ConcurrentDictionary<string, ICameraProvider> _providers = new();
    private readonly object _lock = new();
    private bool _disposed;

    public CameraManager()
    {
    }

    /// <summary>
    /// 枚举所有可用相机
    /// </summary>
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
            IsConnected = false
        });

        return Task.FromResult(cameraInfos);
    }

    /// <summary>
    /// 获取或创建相机
    /// </summary>
    public Task<ICamera> GetOrCreateCameraAsync(string cameraId)
    {
        if (_cameras.TryGetValue(cameraId, out var existingCamera))
        {
            return Task.FromResult(existingCamera);
        }

        // 自动检测相机类型并创建
        var provider = CameraProviderFactory.AutoDetect(cameraId);
        if (provider == null)
        {
            throw new InvalidOperationException($"无法检测到相机: {cameraId}");
        }

        // 打开相机
        if (!provider.Open(cameraId))
        {
            throw new InvalidOperationException($"无法打开相机: {cameraId}");
        }

        // 创建适配器
        var cameraAdapter = new CameraProviderAdapter(cameraId, provider);
        _cameras[cameraId] = cameraAdapter;
        _providers[cameraId] = provider;

        Debug.WriteLine($"[CameraManager] Camera created and opened: {cameraId}");
        return Task.FromResult<ICamera>(cameraAdapter);
    }

    /// <summary>
    /// 获取所有已连接的相机
    /// </summary>
    public IReadOnlyList<ICamera> GetConnectedCameras()
    {
        return _cameras.Values.ToList();
    }

    /// <summary>
    /// 打开相机（ICameraManager接口实现）
    /// </summary>
    public Task<ICamera> OpenCameraAsync(string cameraId)
    {
        return GetOrCreateCameraAsync(cameraId);
    }

    /// <summary>
    /// 关闭相机
    /// </summary>
    public Task CloseCameraAsync(string cameraId)
    {
        if (_cameras.TryRemove(cameraId, out var camera))
        {
            camera.Dispose();
        }

        if (_providers.TryRemove(cameraId, out var provider))
        {
            provider.Dispose();
        }

        Debug.WriteLine($"[CameraManager] Camera closed: {cameraId}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取相机
    /// </summary>
    public ICamera? GetCamera(string cameraId)
    {
        _cameras.TryGetValue(cameraId, out var camera);
        return camera;
    }

    /// <summary>
    /// 断开所有相机
    /// </summary>
    public Task DisconnectAllAsync()
    {
        foreach (var camera in _cameras.Values)
        {
            camera.Dispose();
        }
        _cameras.Clear();

        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }
        _providers.Clear();

        Debug.WriteLine("[CameraManager] All cameras disconnected");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            DisconnectAllAsync().Wait();
        }

        _disposed = true;
    }

    ~CameraManager() => Dispose(false);
}

/// <summary>
/// 相机提供器适配器 - 将ICameraProvider适配为ICamera接口
/// </summary>
public class CameraProviderAdapter : IIndustrialCamera
{
    private readonly string _cameraId;
    private readonly ICameraProvider _provider;
    private bool _isAcquiring;
    private Func<byte[], Task>? _frameCallback;
    private CancellationTokenSource? _acquisitionCts;

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

    public Task ConnectAsync()
    {
        // 已在构造函数中打开
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _provider.Close();
        return Task.CompletedTask;
    }

    public async Task<byte[]> AcquireSingleFrameAsync()
    {
        if (!_provider.IsConnected)
        {
            throw new InvalidOperationException("相机未连接");
        }

        if (!_provider.IsGrabbing)
        {
            _provider.StartGrabbing();
        }

        var frame = _provider.GetFrame(5000);
        if (frame == null)
        {
            throw new TimeoutException("获取图像超时");
        }

        // 复制数据
        byte[] data = new byte[frame.Size];
        Marshal.Copy(frame.DataPtr, data, 0, frame.Size);

        // 触发事件
        FrameReceived?.Invoke(this, new CameraFrameReceivedEventArgs
        {
            ImageData = data,
            Width = frame.Width,
            Height = frame.Height,
            Timestamp = DateTime.UtcNow
        });

        return await Task.FromResult(data);
    }

    public Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback)
    {
        if (_isAcquiring) return Task.CompletedTask;

        _frameCallback = frameCallback;
        _isAcquiring = true;
        _acquisitionCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            if (!_provider.IsGrabbing)
            {
                _provider.StartGrabbing();
            }

            while (!_acquisitionCts.Token.IsCancellationRequested)
            {
                try
                {
                    var frame = _provider.GetFrame(1000);
                    if (frame != null)
                    {
                        byte[] data = new byte[frame.Size];
                        Marshal.Copy(frame.DataPtr, data, 0, frame.Size);

                        if (_frameCallback != null)
                        {
                            await _frameCallback(data);
                        }

                        FrameReceived?.Invoke(this, new CameraFrameReceivedEventArgs
                        {
                            ImageData = data,
                            Width = frame.Width,
                            Height = frame.Height,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CameraProviderAdapter] Acquisition error: {ex.Message}");
                }
            }
        }, _acquisitionCts.Token);

        return Task.CompletedTask;
    }

    public Task StopContinuousAcquisitionAsync()
    {
        _isAcquiring = false;
        _acquisitionCts?.Cancel();
        _provider.StopGrabbing();
        return Task.CompletedTask;
    }

    public Task SetExposureTimeAsync(double exposureTime)
    {
        _provider.SetExposure(exposureTime);
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double gain)
    {
        _provider.SetGain(gain);
        return Task.CompletedTask;
    }

    public Task SetTriggerModeAsync(bool isHardwareTrigger)
    {
        // true = 硬件触发, false = 连续采集(软件触发关闭)
        _provider.SetTriggerMode(!isHardwareTrigger);
        return Task.CompletedTask;
    }

    public Task ExecuteSoftwareTriggerAsync()
    {
        _provider.ExecuteSoftwareTrigger();
        return Task.CompletedTask;
    }

    public CameraParameters GetParameters()
    {
        return new CameraParameters
        {
            Width = 0,
            Height = 0,
            PixelFormat = "RGB8",
            ExposureTime = 0,
            Gain = 0,
            FrameRate = 0
        };
    }

    public void Dispose()
    {
        StopContinuousAcquisitionAsync().Wait();
        _provider.Dispose();
    }
}
