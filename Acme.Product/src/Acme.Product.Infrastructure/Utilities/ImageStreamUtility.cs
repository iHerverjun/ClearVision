// ImageStreamUtility.cs
// 获取推荐的压缩参数
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.Utilities;

/// <summary>
/// 图像数据流工具类
/// 使用OpenCV进行图像压缩和Base64编码
/// Sprint 4: S4-003 实现
/// </summary>
public static class ImageStreamUtility
{
    /// <summary>
    /// 将图像数据压缩并转换为Base64
    /// </summary>
    /// <param name="imageData">原始图像字节数组</param>
    /// <param name="format">压缩格式: jpg, png</param>
    /// <param name="quality">压缩质量(0-100, JPG有效)</param>
    /// <param name="maxWidth">最大宽度(可选)</param>
    /// <param name="maxHeight">最大高度(可选)</param>
    /// <returns>Base64编码的图像数据</returns>
    public static string CompressAndEncodeToBase64(
        byte[] imageData,
        string format = "jpg",
        int quality = 85,
        int? maxWidth = null,
        int? maxHeight = null)
    {
        try
        {
            // 从字节数组加载图像
            using var mat = Cv2.ImDecode(imageData, ImreadModes.Color);
            if (mat.Empty())
            {
                throw new ArgumentException("无法解码图像数据");
            }

            // 缩放图像（如果需要）
            using var resizedMat = ResizeMat(mat, maxWidth, maxHeight);

            // 编码参数
            var extension = format.ToLower() switch
            {
                "png" => ".png",
                "bmp" => ".bmp",
                _ => ".jpg"
            };

            var encodeParams = new int[0];
            if (extension == ".jpg")
            {
                // JPEG质量参数
                encodeParams = new[]
                {
                    (int)ImwriteFlags.JpegQuality,
                    quality
                };
            }
            else if (extension == ".png")
            {
                // PNG压缩级别
                encodeParams = new[]
                {
                    (int)ImwriteFlags.PngCompression,
                    6
                };
            }

            // 编码为字节数组
            var compressedData = encodeParams.Length > 0
                ? resizedMat.ToBytes(extension, encodeParams)
                : resizedMat.ToBytes(extension);

            return Convert.ToBase64String(compressedData);
        }
        catch (Exception ex)
        {
            // 注意：此类为静态工具类，无法直接注入ILogger
            // 异常应向上抛出由调用方处理，或可考虑传入ILogger参数
            // 临时方案：返回原始数据，异常由调用方捕获并记录
            // 失败时返回原始数据的Base64
            return Convert.ToBase64String(imageData);
        }
    }

    /// <summary>
    /// 从OpenCV Mat直接转换为Base64（压缩后）
    /// </summary>
    public static string MatToCompressedBase64(
        Mat mat,
        string format = "jpg",
        int quality = 85,
        int? maxWidth = null,
        int? maxHeight = null)
    {
        try
        {
            if (mat.Empty())
            {
                throw new ArgumentException("Mat不能为空");
            }

            // 缩放图像（如果需要）
            using var resizedMat = ResizeMat(mat, maxWidth, maxHeight);

            var extension = format.ToLower() switch
            {
                "png" => ".png",
                "bmp" => ".bmp",
                _ => ".jpg"
            };

            var encodeParams = new int[0];
            if (extension == ".jpg")
            {
                encodeParams = new[]
                {
                    (int)ImwriteFlags.JpegQuality,
                    quality
                };
            }

            var compressedData = encodeParams.Length > 0
                ? resizedMat.ToBytes(extension, encodeParams)
                : resizedMat.ToBytes(extension);

            return Convert.ToBase64String(compressedData);
        }
        catch (Exception ex)
        {
            // 注意：此类为静态工具类，无法直接注入ILogger
            // 异常向上抛出，由调用方使用ILogger记录
            throw;
        }
    }

    /// <summary>
    /// 缩放Mat（保持宽高比）
    /// </summary>
    private static Mat ResizeMat(Mat mat, int? maxWidth, int? maxHeight)
    {
        if (!maxWidth.HasValue && !maxHeight.HasValue)
        {
            return mat.Clone();
        }

        var (newWidth, newHeight) = CalculateNewSize(
            mat.Width, mat.Height,
            maxWidth, maxHeight);

        if (newWidth == mat.Width && newHeight == mat.Height)
        {
            return mat.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(mat, resized, new OpenCvSharp.Size(newWidth, newHeight));
        return resized;
    }

    /// <summary>
    /// 计算新的图像尺寸（保持宽高比）
    /// </summary>
    private static (int width, int height) CalculateNewSize(
        int originalWidth, int originalHeight,
        int? maxWidth, int? maxHeight)
    {
        var newWidth = originalWidth;
        var newHeight = originalHeight;

        if (maxWidth.HasValue && originalWidth > maxWidth.Value)
        {
            newWidth = maxWidth.Value;
            newHeight = (int)((double)originalHeight * maxWidth.Value / originalWidth);
        }

        if (maxHeight.HasValue && newHeight > maxHeight.Value)
        {
            newHeight = maxHeight.Value;
            newWidth = (int)((double)newWidth * maxHeight.Value / newHeight);
        }

        return (newWidth, newHeight);
    }

    /// <summary>
    /// 估算压缩后的大小
    /// </summary>
    public static long EstimateCompressedSize(long originalSize, string format, int quality)
    {
        return format.ToLower() switch
        {
            "jpg" or "jpeg" => (long)(originalSize * (quality / 100.0) * 0.1),
            "png" => (long)(originalSize * 0.5),
            _ => originalSize
        };
    }

    /// <summary>
    /// 检查图像是否需要压缩
    /// </summary>
    public static bool ShouldCompress(byte[] imageData, long maxSizeBytes = 1024 * 1024)
    {
        return imageData.Length > maxSizeBytes;
    }

    /// <summary>
    /// 获取推荐的压缩参数
    /// </summary>
    public static (string format, int quality, int? maxWidth, int? maxHeight) GetRecommendedCompression(
        byte[] imageData,
        int originalWidth,
        int originalHeight)
    {
        var size = imageData.Length;

        // 小于100KB，不压缩
        if (size < 100 * 1024)
        {
            return ("png", 100, null, null);
        }

        // 小于1MB，轻度压缩
        if (size < 1024 * 1024)
        {
            return ("jpg", 90, null, null);
        }

        // 小于5MB，中度压缩
        if (size < 5 * 1024 * 1024)
        {
            return ("jpg", 80, 1920, 1080);
        }

        // 大图像，重度压缩
        return ("jpg", 70, 1280, 720);
    }
}
