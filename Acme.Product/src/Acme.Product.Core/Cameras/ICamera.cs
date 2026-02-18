// ICamera.cs
// 相机相关接口定义
// 作者：蘅芜君

using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Cameras;

/// <summary>
/// 相机接口
/// </summary>
public interface ICamera : IDisposable
{
    string CameraId { get; }
    string Name { get; }
    bool IsConnected { get; }
    bool IsAcquiring { get; }

    Task ConnectAsync();
    Task DisconnectAsync();
    Task<byte[]> AcquireSingleFrameAsync();
    Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback);
    Task StopContinuousAcquisitionAsync();
    Task SetExposureTimeAsync(double exposureTime);
    Task SetGainAsync(double gain);
    CameraParameters GetParameters();
}

/// <summary>
/// 相机参数
/// </summary>
public class CameraParameters
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string PixelFormat { get; set; } = "RGB8";
    public double ExposureTime { get; set; }
    public double Gain { get; set; }
    public double FrameRate { get; set; }
}

/// <summary>
/// 相机枚举信息
/// </summary>
public class CameraInfo
{
    public string CameraId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? ConnectionType { get; set; }
    public bool IsConnected { get; set; }
}

/// <summary>
/// 相机管理器接口
/// </summary>
public interface ICameraManager
{
    Task<IEnumerable<CameraInfo>> EnumerateCamerasAsync();
    Task<ICamera> GetOrCreateCameraAsync(string cameraId);
    Task<ICamera> OpenCameraAsync(string cameraId);
    Task CloseCameraAsync(string cameraId);
    ICamera? GetCamera(string cameraId);
    Task DisconnectAllAsync();

    // 绑定管理
    void LoadBindings(List<CameraBindingConfig> bindings, string activeCameraId);
    List<CameraBindingConfig> GetBindings();
    void UpdateBindings(List<CameraBindingConfig> bindings, string activeCameraId);
    Task<ICamera> GetOrCreateByBindingAsync(string bindingId);
}
