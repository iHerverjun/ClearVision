// MockCamera.cs
// 模拟相机实现（用于测试）
// 作者：蘅芜君

using Acme.Product.Core.Cameras;

namespace Acme.Product.Infrastructure.Cameras;

/// <summary>
/// 模拟相机实现（用于测试）
/// </summary>
public class MockCamera : ICamera
{
    private bool _isConnected = false;
    private bool _isAcquiring = false;
    private readonly Random _random = new();
    private Func<byte[], Task>? _frameCallback;
    private CancellationTokenSource? _cancellationTokenSource;

    public string CameraId { get; }
    public string Name { get; }
    public bool IsConnected => _isConnected;
    public bool IsAcquiring => _isAcquiring;

    public CameraParameters Parameters { get; private set; } = new()
    {
        Width = 1920,
        Height = 1080,
        PixelFormat = "RGB8",
        ExposureTime = 10000,
        Gain = 1.0,
        FrameRate = 30
    };

    public MockCamera(string cameraId, string name)
    {
        CameraId = cameraId;
        Name = name;
    }

    public Task ConnectAsync()
    {
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

        // 生成模拟图像数据（纯色图像）
        var imageData = GenerateMockImage();
        return Task.FromResult(imageData);
    }

    public Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback)
    {
        if (!_isConnected)
            throw new InvalidOperationException("相机未连接");

        _frameCallback = frameCallback;
        _isAcquiring = true;
        _cancellationTokenSource = new CancellationTokenSource();

        // 启动后台任务模拟连续采集
        _ = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var imageData = GenerateMockImage();
                if (_frameCallback != null)
                {
                    await _frameCallback(imageData);
                }
                await Task.Delay(1000 / (int)Parameters.FrameRate, _cancellationTokenSource.Token);
            }
        }, _cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopContinuousAcquisitionAsync()
    {
        _cancellationTokenSource?.Cancel();
        _isAcquiring = false;
        await Task.Delay(100); // 等待采集循环停止
    }

    public Task SetExposureTimeAsync(double exposureTime)
    {
        Parameters.ExposureTime = exposureTime;
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double gain)
    {
        Parameters.Gain = gain;
        return Task.CompletedTask;
    }

    public CameraParameters GetParameters() => Parameters;

    private byte[] GenerateMockImage()
    {
        // 生成简单的模拟图像数据（纯色 + 随机噪声）
        var width = Parameters.Width;
        var height = Parameters.Height;
        var imageData = new byte[width * height * 3];

        // 基础颜色（灰色）
        byte baseColor = (byte)(_random.Next(100, 200));

        for (int i = 0; i < imageData.Length; i += 3)
        {
            // 添加一些随机噪声
            var noise = (byte)_random.Next(-20, 20);
            imageData[i] = (byte)Math.Clamp(baseColor + noise, 0, 255);     // R
            imageData[i + 1] = (byte)Math.Clamp(baseColor + noise, 0, 255); // G
            imageData[i + 2] = (byte)Math.Clamp(baseColor + noise, 0, 255); // B
        }

        return imageData;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
