// ImageAcquisitionService.cs
// 释放资源
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Exceptions;
using Acme.Product.Infrastructure.Utilities;
using OpenCvSharp;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 图像采集服务实现 - 简化版
/// Sprint 4: S4-005 实现
/// </summary>
public class ImageAcquisitionService : IImageAcquisitionService, IDisposable
{
    private readonly ICameraManager _cameraManager;
    private readonly ILogger<ImageAcquisitionService> _logger;
    private readonly Dictionary<Guid, Mat> _imageCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _continuousAcquisitionTokens = new();
    private bool _disposed;

    public ImageAcquisitionService(ICameraManager cameraManager, ILogger<ImageAcquisitionService> logger)
    {
        _cameraManager = cameraManager;
        _logger = logger;
    }

    public async Task<ImageDto> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("图像文件不存在", filePath);
        }

        var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var fileName = Path.GetFileName(filePath);

        return await LoadFromBytesAsync(imageData, fileName, cancellationToken);
    }

    public Task<ImageDto> LoadFromBytesAsync(byte[] imageData, string? fileName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var mat = Cv2.ImDecode(imageData, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                throw new ArgumentException("无法解码图像数据");
            }

            var imageId = Guid.NewGuid();

            lock (_imageCache)
            {
                _imageCache[imageId] = mat.Clone();
            }

            var format = GetFormatFromFileName(fileName) ?? "unknown";

            return Task.FromResult(new ImageDto
            {
                Id = imageId,
                Name = fileName ?? $"{imageId}.{format}",
                Format = format,
                Width = mat.Width,
                Height = mat.Height,
                DataBase64 = Convert.ToBase64String(imageData),
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载图像失败: {ex.Message}", ex);
        }
    }

    public Task<ImageDto> LoadFromBase64Async(string base64String, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageData = Convert.FromBase64String(base64String);
            return LoadFromBytesAsync(imageData, null, cancellationToken);
        }
        catch (FormatException)
        {
            throw new ArgumentException("无效的Base64字符串");
        }
    }

    public async Task<ImageDto> AcquireFromCameraAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);

        // 获取相机实例
        var camera = _cameraManager.GetCamera(cameraId);
        if (camera == null)
        {
            throw new CameraException($"相机未找到或未连接: {cameraId}", cameraId);
        }

        if (!camera.IsConnected)
        {
            throw new CameraException($"相机未连接: {cameraId}", cameraId);
        }

        try
        {
            // 采集单帧图像
            var frameData = await camera.AcquireSingleFrameAsync();

            // 解码图像数据
            using var mat = Cv2.ImDecode(frameData, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                throw new ImageProcessingException("无法解码相机采集的图像数据");
            }

            var imageId = Guid.NewGuid();

            // 缓存图像
            lock (_imageCache)
            {
                _imageCache[imageId] = mat.Clone();
            }

            return new ImageDto
            {
                Id = imageId,
                Name = $"Camera_{cameraId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Format = "png",
                Width = mat.Width,
                Height = mat.Height,
                DataBase64 = Convert.ToBase64String(frameData),
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not CameraException and not ImageProcessingException)
        {
            throw new CameraException($"采集图像失败: {ex.Message}", cameraId);
        }
    }

    public async Task StartContinuousAcquisitionAsync(string cameraId, int frameRate, Func<ImageDto, Task> onFrameAcquired, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameRate, 0);

        // 检查是否已有该相机的连续采集任务
        if (_continuousAcquisitionTokens.ContainsKey(cameraId))
        {
            throw new CameraException($"相机 {cameraId} 已经在连续采集模式中", cameraId);
        }

        // 获取相机实例
        var camera = _cameraManager.GetCamera(cameraId);
        if (camera == null)
        {
            throw new CameraException($"相机未找到或未连接: {cameraId}", cameraId);
        }

        if (!camera.IsConnected)
        {
            throw new CameraException($"相机未连接: {cameraId}", cameraId);
        }

        // 创建取消令牌源
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _continuousAcquisitionTokens[cameraId] = cts;

        try
        {
            // 计算帧间隔（毫秒）
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / frameRate);

            // 启动连续采集循环
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var startTime = DateTime.UtcNow;

                        // 采集单帧
                        var frameData = await camera.AcquireSingleFrameAsync();

                        // 解码图像
                        using var mat = Cv2.ImDecode(frameData, ImreadModes.Unchanged);
                        if (!mat.Empty())
                        {
                            var imageId = Guid.NewGuid();

                            // 缓存图像
                            lock (_imageCache)
                            {
                                _imageCache[imageId] = mat.Clone();
                            }

                            var imageDto = new ImageDto
                            {
                                Id = imageId,
                                Name = $"Camera_{cameraId}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}",
                                Format = "png",
                                Width = mat.Width,
                                Height = mat.Height,
                                DataBase64 = Convert.ToBase64String(frameData),
                                CreatedAt = DateTime.UtcNow
                            };

                            // 调用回调函数
                            await onFrameAcquired(imageDto);
                        }

                        // 控制帧率
                        var elapsed = DateTime.UtcNow - startTime;
                        var delay = frameInterval - elapsed;
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但继续采集
                        _logger.LogWarning(ex, "连续采集错误: {Message}. 将在 {Interval}ms 后重试", ex.Message, frameInterval.TotalMilliseconds);
                        await Task.Delay(frameInterval, cts.Token);
                    }
                }
            }, cts.Token);
        }
        catch
        {
            _continuousAcquisitionTokens.TryRemove(cameraId, out _);
            cts.Dispose();
            throw;
        }
    }

    public Task StopContinuousAcquisitionAsync(string cameraId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);

        if (_continuousAcquisitionTokens.TryRemove(cameraId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetSupportedFormatsAsync()
    {
        var formats = new[] { "jpg", "jpeg", "png", "bmp", "tiff", "webp" };
        return Task.FromResult<IEnumerable<string>>(formats);
    }

    public Task<ImageValidationResult> ValidateImageFileAsync(string filePath)
    {
        var result = new ImageValidationResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.IsValid = false;
                result.Errors.Add("文件不存在");
                return Task.FromResult(result);
            }

            var fileInfo = new FileInfo(filePath);
            result.FileSize = fileInfo.Length;

            if (fileInfo.Length > 100 * 1024 * 1024)
            {
                result.Warnings.Add("文件过大，可能影响处理性能");
            }

            using var mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                result.IsValid = false;
                result.Errors.Add("无法读取图像文件，格式可能不支持");
                return Task.FromResult(result);
            }

            result.IsValid = true;
            result.Format = Path.GetExtension(filePath).TrimStart('.').ToLower();
            result.Width = mat.Width;
            result.Height = mat.Height;
            result.Channels = mat.Channels();

            if (mat.Width < 10 || mat.Height < 10)
            {
                result.Warnings.Add("图像尺寸过小");
            }

            if (mat.Width > 10000 || mat.Height > 10000)
            {
                result.Warnings.Add("图像尺寸过大，可能影响处理性能");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"验证失败: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    public Task<ImageDto> PreprocessAsync(Guid imageId, ImagePreprocessOptions options)
    {
        // 从缓存获取图像
        Mat? sourceMat = null;
        lock (_imageCache)
        {
            if (!_imageCache.TryGetValue(imageId, out sourceMat))
            {
                throw new ImageProcessingException($"图像不存在: {imageId}");
            }
        }

        try
        {
            using var processedMat = sourceMat.Clone();
            Mat? resultMat = null;

            try
            {
                // 转换为灰度图
                if (options.ConvertToGrayscale && processedMat.Channels() > 1)
                {
                    using var grayMat = new Mat();
                    Cv2.CvtColor(processedMat, grayMat, ColorConversionCodes.BGR2GRAY);
                    resultMat = grayMat.Clone();
                }
                else
                {
                    resultMat = processedMat.Clone();
                }

                // 调整大小
                if (options.TargetWidth.HasValue || options.TargetHeight.HasValue)
                {
                    var targetWidth = options.TargetWidth ?? resultMat.Width;
                    var targetHeight = options.TargetHeight ?? resultMat.Height;

                    if (options.KeepAspectRatio)
                    {
                        // 保持宽高比
                        var scale = Math.Min(
                            (double)targetWidth / resultMat.Width,
                            (double)targetHeight / resultMat.Height);
                        targetWidth = (int)(resultMat.Width * scale);
                        targetHeight = (int)(resultMat.Height * scale);
                    }

                    using var resizedMat = new Mat();
                    Cv2.Resize(resultMat, resizedMat, new Size(targetWidth, targetHeight), 0, 0, InterpolationFlags.Linear);
                    resultMat.Dispose();
                    resultMat = resizedMat.Clone();
                }

                // 应用滤波
                if (options.ApplyFilter)
                {
                    using var filteredMat = new Mat();
                    var kernelSize = options.FilterKernelSize;
                    if (kernelSize % 2 == 0)
                        kernelSize++; // 确保奇数

                    switch (options.FilterType?.ToLower())
                    {
                        case "gaussian":
                            Cv2.GaussianBlur(resultMat, filteredMat, new Size(kernelSize, kernelSize), 0);
                            break;
                        case "median":
                            Cv2.MedianBlur(resultMat, filteredMat, kernelSize);
                            break;
                        case "bilateral":
                            Cv2.BilateralFilter(resultMat, filteredMat, kernelSize, kernelSize * 2, kernelSize / 2);
                            break;
                        default:
                            Cv2.GaussianBlur(resultMat, filteredMat, new Size(kernelSize, kernelSize), 0);
                            break;
                    }
                    resultMat.Dispose();
                    resultMat = filteredMat.Clone();
                }

                // 旋转
                if (options.RotationAngle.HasValue && Math.Abs(options.RotationAngle.Value) > 0.001)
                {
                    using var rotatedMat = new Mat();
                    var center = new Point2f(resultMat.Width / 2f, resultMat.Height / 2f);
                    var rotationMatrix = Cv2.GetRotationMatrix2D(center, options.RotationAngle.Value, 1.0);
                    Cv2.WarpAffine(resultMat, rotatedMat, rotationMatrix, resultMat.Size());
                    resultMat.Dispose();
                    resultMat = rotatedMat.Clone();
                }

                // 翻转
                if (options.FlipHorizontal || options.FlipVertical)
                {
                    using var flippedMat = new Mat();
                    var flipCode = options.FlipHorizontal && options.FlipVertical ? -1 :
                                   options.FlipHorizontal ? 1 : 0;
                    Cv2.Flip(resultMat, flippedMat, (FlipMode)flipCode);
                    resultMat.Dispose();
                    resultMat = flippedMat.Clone();
                }

                // 归一化
                if (options.Normalize)
                {
                    using var normalizedMat = new Mat();
                    Cv2.Normalize(resultMat, normalizedMat, 0, 1, NormTypes.MinMax);
                    resultMat.Dispose();
                    resultMat = normalizedMat.Clone();
                }

                // 生成新的图像ID并缓存
                var newImageId = Guid.NewGuid();
                lock (_imageCache)
                {
                    _imageCache[newImageId] = resultMat.Clone();
                }

                // 编码为字节数组
                var format = processedMat.Channels() == 1 ? ".png" : ".png";
                var encodedData = resultMat.ToBytes(format);

                var result = new ImageDto
                {
                    Id = newImageId,
                    Name = $"Preprocessed_{imageId}",
                    Format = "png",
                    Width = resultMat.Width,
                    Height = resultMat.Height,
                    DataBase64 = Convert.ToBase64String(encodedData),
                    CreatedAt = DateTime.UtcNow
                };

                resultMat.Dispose();
                return Task.FromResult(result);
            }
            catch
            {
                resultMat?.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is not ImageProcessingException)
        {
            throw new ImageProcessingException($"预处理图像失败: {ex.Message}", ex);
        }
    }

    public Task<string> SaveToFileAsync(Guid imageId, string filePath, string format = "png", int quality = 95)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // 从缓存获取图像
        Mat? sourceMat = null;
        lock (_imageCache)
        {
            if (!_imageCache.TryGetValue(imageId, out sourceMat))
            {
                throw new ImageProcessingException($"图像不存在: {imageId}");
            }
        }

        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 根据格式设置保存参数
            var saveFormat = format.ToLower() switch
            {
                "jpg" or "jpeg" => ".jpg",
                "png" => ".png",
                "bmp" => ".bmp",
                "tiff" or "tif" => ".tiff",
                "webp" => ".webp",
                _ => ".png"
            };

            // 如果是JPEG格式，设置质量参数
            if (saveFormat == ".jpg" || saveFormat == ".jpeg")
            {
                var jpegParams = new int[]
                {
                    (int)ImwriteFlags.JpegQuality,
                    Math.Clamp(quality, 1, 100)
                };
                Cv2.ImWrite(filePath, sourceMat, jpegParams);
            }
            else if (saveFormat == ".png")
            {
                // PNG使用压缩级别
                var pngParams = new int[]
                {
                    (int)ImwriteFlags.PngCompression,
                    Math.Clamp((100 - quality) / 10, 0, 9)
                };
                Cv2.ImWrite(filePath, sourceMat, pngParams);
            }
            else
            {
                Cv2.ImWrite(filePath, sourceMat);
            }

            return Task.FromResult(filePath);
        }
        catch (Exception ex) when (ex is not ImageProcessingException)
        {
            throw new ImageProcessingException($"保存图像失败: {ex.Message}", ex);
        }
    }

    public Task<ImageInfoDto?> GetImageInfoAsync(Guid imageId)
    {
        Mat? sourceMat = null;
        lock (_imageCache)
        {
            if (!_imageCache.TryGetValue(imageId, out sourceMat))
            {
                return Task.FromResult<ImageInfoDto?>(null);
            }
        }

        var info = new ImageInfoDto
        {
            Id = imageId,
            Format = "raw",
            Width = sourceMat.Width,
            Height = sourceMat.Height,
            Channels = sourceMat.Channels(),
            FileSize = sourceMat.Total() * sourceMat.ElemSize(),
            CreatedAt = DateTime.UtcNow,
            IsInMemory = true
        };

        return Task.FromResult<ImageInfoDto?>(info);
    }

    public Task ReleaseImageAsync(Guid imageId)
    {
        lock (_imageCache)
        {
            if (_imageCache.TryGetValue(imageId, out var mat))
            {
                mat.Dispose();
                _imageCache.Remove(imageId);
            }
        }
        return Task.CompletedTask;
    }

    private string? GetFormatFromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        var ext = Path.GetExtension(fileName).ToLower();
        return ext.TrimStart('.');
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // 停止所有连续采集任务
        foreach (var cts in _continuousAcquisitionTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _continuousAcquisitionTokens.Clear();

        // 释放所有缓存的图像
        lock (_imageCache)
        {
            foreach (var mat in _imageCache.Values)
            {
                mat.Dispose();
            }
            _imageCache.Clear();
        }

        _cacheLock.Dispose();
        _disposed = true;
    }
}
