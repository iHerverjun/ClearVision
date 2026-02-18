// FileCamera.cs
// 文件相机实现（从文件加载图像）
// 作者：蘅芜君

using Acme.Product.Core.Cameras;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 文件相机实现（从文件加载图像）
/// </summary>
public class FileCamera : ICamera
{
    private bool _isConnected = false;
    private string _imagePath = string.Empty;

    public string CameraId { get; }
    public string Name { get; }
    public bool IsConnected => _isConnected;
    public bool IsAcquiring => false; // 文件相机不支持连续采集

    public CameraParameters Parameters { get; private set; } = new()
    {
        Width = 1920,
        Height = 1080,
        PixelFormat = "RGB8",
        ExposureTime = 0,
        Gain = 0,
        FrameRate = 0
    };

    public FileCamera(string cameraId, string name, string imagePath)
    {
        CameraId = cameraId;
        Name = name;
        _imagePath = imagePath;
    }

    public Task ConnectAsync()
    {
        if (!File.Exists(_imagePath))
            throw new FileNotFoundException("图像文件不存在", _imagePath);

        _isConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<byte[]> AcquireSingleFrameAsync()
    {
        if (!_isConnected)
            throw new InvalidOperationException("相机未连接");

        if (!File.Exists(_imagePath))
            throw new FileNotFoundException("图像文件不存在", _imagePath);

        var imageData = File.ReadAllBytes(_imagePath);
        return Task.FromResult(imageData);
    }

    public Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback)
    {
        throw new NotSupportedException("文件相机不支持连续采集");
    }

    public Task StopContinuousAcquisitionAsync()
    {
        return Task.CompletedTask;
    }

    public Task SetExposureTimeAsync(double exposureTime)
    {
        // 文件相机不支持设置曝光时间
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double gain)
    {
        // 文件相机不支持设置增益
        return Task.CompletedTask;
    }

    public CameraParameters GetParameters() => Parameters;

    public void Dispose()
    {
        _isConnected = false;
    }
}
