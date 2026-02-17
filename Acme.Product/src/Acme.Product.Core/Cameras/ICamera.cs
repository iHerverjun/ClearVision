namespace Acme.Product.Core.Cameras;

/// <summary>
/// 相机接口
/// </summary>
public interface ICamera : IDisposable
{
    /// <summary>
    /// 相机ID
    /// </summary>
    string CameraId { get; }

    /// <summary>
    /// 相机名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 是否正在采集
    /// </summary>
    bool IsAcquiring { get; }

    /// <summary>
    /// 连接相机
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 采集单帧图像
    /// </summary>
    /// <returns>图像数据（字节数组）</returns>
    Task<byte[]> AcquireSingleFrameAsync();

    /// <summary>
    /// 开始连续采集
    /// </summary>
    /// <param name="frameCallback">帧回调函数</param>
    Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback);

    /// <summary>
    /// 停止连续采集
    /// </summary>
    Task StopContinuousAcquisitionAsync();

    /// <summary>
    /// 设置曝光时间
    /// </summary>
    /// <param name="exposureTime">曝光时间（微秒）</param>
    Task SetExposureTimeAsync(double exposureTime);

    /// <summary>
    /// 设置增益
    /// </summary>
    /// <param name="gain">增益值</param>
    Task SetGainAsync(double gain);

    /// <summary>
    /// 获取相机参数
    /// </summary>
    CameraParameters GetParameters();
}

/// <summary>
/// 相机参数
/// </summary>
public class CameraParameters
{
    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 像素格式
    /// </summary>
    public string PixelFormat { get; set; } = "RGB8";

    /// <summary>
    /// 曝光时间（微秒）
    /// </summary>
    public double ExposureTime { get; set; }

    /// <summary>
    /// 增益
    /// </summary>
    public double Gain { get; set; }

    /// <summary>
    /// 帧率（FPS）
    /// </summary>
    public double FrameRate { get; set; }
}

/// <summary>
/// 相机信息
/// </summary>
public class CameraInfo
{
    /// <summary>
    /// 相机ID
    /// </summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>
    /// 相机名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 制造商
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// 型号
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 连接类型（USB3/GigE/等）
    /// </summary>
    public string? ConnectionType { get; set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; set; }
}

/// <summary>
/// 相机管理器接口
/// </summary>
public interface ICameraManager
{
    /// <summary>
    /// 枚举可用相机
    /// </summary>
    Task<IEnumerable<CameraInfo>> EnumerateCamerasAsync();

    /// <summary>
    /// 获取或创建相机
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    Task<ICamera> GetOrCreateCameraAsync(string cameraId);

    /// <summary>
    /// 打开相机
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    Task<ICamera> OpenCameraAsync(string cameraId);

    /// <summary>
    /// 关闭相机
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    Task CloseCameraAsync(string cameraId);

    /// <summary>
    /// 获取已打开的相机
    /// </summary>
    ICamera? GetCamera(string cameraId);

    /// <summary>
    /// 断开所有相机
    /// </summary>
    Task DisconnectAllAsync();
}
