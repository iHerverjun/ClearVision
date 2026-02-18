// IImageAcquisitionService.cs
// 图像信息DTO
// 作者：蘅芜君

using Acme.Product.Application.DTOs;

namespace Acme.Product.Application.Services;

/// <summary>
/// 图像采集服务接口
/// Sprint 4: S4-005 实现
/// 
/// 功能：
/// - 从文件加载图像
/// - 从相机采集图像
/// - 图像格式转换和预处理
/// - 图像数据缓存管理
/// </summary>
public interface IImageAcquisitionService
{
    /// <summary>
    /// 从文件路径加载图像
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像数据传输对象</returns>
    Task<ImageDto> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从字节数组加载图像
    /// </summary>
    /// <param name="imageData">图像字节数组</param>
    /// <param name="fileName">文件名（用于确定格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像数据传输对象</returns>
    Task<ImageDto> LoadFromBytesAsync(byte[] imageData, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从Base64字符串加载图像
    /// </summary>
    /// <param name="base64String">Base64编码的图像数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像数据传输对象</returns>
    Task<ImageDto> LoadFromBase64Async(string base64String, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从相机采集单帧图像
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像数据传输对象</returns>
    Task<ImageDto> AcquireFromCameraAsync(string cameraId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始连续采集（实时模式）
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    /// <param name="frameRate">帧率（FPS）</param>
    /// <param name="onFrameAcquired">帧采集回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>采集任务</returns>
    Task StartContinuousAcquisitionAsync(
        string cameraId,
        int frameRate,
        Func<ImageDto, Task> onFrameAcquired,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止连续采集
    /// </summary>
    /// <param name="cameraId">相机ID</param>
    Task StopContinuousAcquisitionAsync(string cameraId);

    /// <summary>
    /// 获取支持的图像格式列表
    /// </summary>
    /// <returns>支持的格式列表</returns>
    Task<IEnumerable<string>> GetSupportedFormatsAsync();

    /// <summary>
    /// 验证图像文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>验证结果</returns>
    Task<ImageValidationResult> ValidateImageFileAsync(string filePath);

    /// <summary>
    /// 图像预处理
    /// </summary>
    /// <param name="imageId">图像ID</param>
    /// <param name="options">预处理选项</param>
    /// <returns>预处理后的图像</returns>
    Task<ImageDto> PreprocessAsync(Guid imageId, ImagePreprocessOptions options);

    /// <summary>
    /// 保存图像到文件
    /// </summary>
    /// <param name="imageId">图像ID</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="format">保存格式</param>
    /// <param name="quality">质量（0-100，JPEG有效）</param>
    /// <returns>保存的文件路径</returns>
    Task<string> SaveToFileAsync(Guid imageId, string filePath, string format = "png", int quality = 95);

    /// <summary>
    /// 获取图像信息
    /// </summary>
    /// <param name="imageId">图像ID</param>
    /// <returns>图像信息</returns>
    Task<ImageInfoDto?> GetImageInfoAsync(Guid imageId);

    /// <summary>
    /// 释放图像资源
    /// </summary>
    /// <param name="imageId">图像ID</param>
    Task ReleaseImageAsync(Guid imageId);
}

/// <summary>
/// 图像预处理选项
/// </summary>
public class ImagePreprocessOptions
{
    /// <summary>
    /// 目标宽度（可选）
    /// </summary>
    public int? TargetWidth { get; set; }

    /// <summary>
    /// 目标高度（可选）
    /// </summary>
    public int? TargetHeight { get; set; }

    /// <summary>
    /// 保持宽高比
    /// </summary>
    public bool KeepAspectRatio { get; set; } = true;

    /// <summary>
    /// 转换为灰度图
    /// </summary>
    public bool ConvertToGrayscale { get; set; } = false;

    /// <summary>
    /// 归一化（0-1）
    /// </summary>
    public bool Normalize { get; set; } = false;

    /// <summary>
    /// 应用滤波
    /// </summary>
    public bool ApplyFilter { get; set; } = false;

    /// <summary>
    /// 滤波类型：gaussian, median, bilateral
    /// </summary>
    public string FilterType { get; set; } = "gaussian";

    /// <summary>
    /// 滤波核大小
    /// </summary>
    public int FilterKernelSize { get; set; } = 3;

    /// <summary>
    /// 旋转角度（度）
    /// </summary>
    public double? RotationAngle { get; set; }

    /// <summary>
    /// 水平翻转
    /// </summary>
    public bool FlipHorizontal { get; set; } = false;

    /// <summary>
    /// 垂直翻转
    /// </summary>
    public bool FlipVertical { get; set; } = false;
}

/// <summary>
/// 图像验证结果
/// </summary>
public class ImageValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 格式
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 通道数
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告消息
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 图像信息DTO
/// </summary>
public class ImageInfoDto
{
    public Guid Id { get; set; }
    public string? FileName { get; set; }
    public string? Format { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Channels { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsInMemory { get; set; }
}
