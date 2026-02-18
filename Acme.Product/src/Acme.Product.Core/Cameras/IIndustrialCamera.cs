// IIndustrialCamera.cs
// 相机像素格式
// 作者：蘅芜君

namespace Acme.Product.Core.Cameras;

/// <summary>
/// 工业相机扩展接口 - 支持硬件触发和软件触发
/// </summary>
public interface IIndustrialCamera : ICamera
{
    /// <summary>
    /// 设置触发模式
    /// </summary>
    /// <param name="isHardwareTrigger">true=硬件触发, false=连续采集</param>
    Task SetTriggerModeAsync(bool isHardwareTrigger);

    /// <summary>
    /// 执行软件触发（仅在软件触发模式下有效）
    /// </summary>
    Task ExecuteSoftwareTriggerAsync();

    /// <summary>
    /// 帧接收事件
    /// </summary>
    event EventHandler<CameraFrameReceivedEventArgs>? FrameReceived;
}

/// <summary>
/// 相机帧接收事件参数
/// </summary>
public class CameraFrameReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 图像数据
    /// </summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 相机提供器接口 - 用于发现创建设备
/// </summary>
public interface ICameraProvider : IDisposable
{
    /// <summary>
    /// 提供器名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 是否正在采集
    /// </summary>
    bool IsGrabbing { get; }

    /// <summary>
    /// 当前设备信息
    /// </summary>
    CameraDeviceInfo? CurrentDevice { get; }

    /// <summary>
    /// 枚举设备
    /// </summary>
    List<CameraDeviceInfo> EnumerateDevices();

    /// <summary>
    /// 打开设备
    /// </summary>
    bool Open(string serialNumber);

    /// <summary>
    /// 关闭设备
    /// </summary>
    bool Close();

    /// <summary>
    /// 开始采集
    /// </summary>
    bool StartGrabbing();

    /// <summary>
    /// 停止采集
    /// </summary>
    bool StopGrabbing();

    /// <summary>
    /// 获取帧
    /// </summary>
    CameraFrame? GetFrame(int timeoutMs = 1000);

    /// <summary>
    /// 设置曝光时间
    /// </summary>
    bool SetExposure(double microseconds);

    /// <summary>
    /// 设置增益
    /// </summary>
    bool SetGain(double value);

    /// <summary>
    /// 设置触发模式
    /// </summary>
    bool SetTriggerMode(bool softwareTrigger);

    /// <summary>
    /// 执行软件触发
    /// </summary>
    bool ExecuteSoftwareTrigger();
}

/// <summary>
/// 相机设备信息
/// </summary>
public class CameraDeviceInfo
{
    /// <summary>
    /// 序列号
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// 制造商
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// 型号
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 用户定义名称
    /// </summary>
    public string UserDefinedName { get; set; } = string.Empty;

    /// <summary>
    /// 接口类型（USB3/GigE等）
    /// </summary>
    public string InterfaceType { get; set; } = string.Empty;
}

/// <summary>
/// 相机帧数据
/// </summary>
public class CameraFrame
{
    /// <summary>
    /// 数据指针
    /// </summary>
    public IntPtr DataPtr { get; set; }

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 数据大小
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// 像素格式
    /// </summary>
    public CameraPixelFormat PixelFormat { get; set; }

    /// <summary>
    /// 帧号
    /// </summary>
    public ulong FrameNumber { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public ulong Timestamp { get; set; }

    /// <summary>
    /// 是否需要原生释放
    /// </summary>
    public bool NeedsNativeRelease { get; set; }
}

/// <summary>
/// 相机像素格式
/// </summary>
public enum CameraPixelFormat
{
    Unknown,
    Mono8,
    RGB8,
    BGR8,
    BayerRG8,
    BayerGB8,
    BayerGR8,
    BayerBG8
}
