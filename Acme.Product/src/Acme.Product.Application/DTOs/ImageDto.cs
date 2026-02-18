// ImageDto.cs
// 图像数据（Base64编码）
// 作者：蘅芜君

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 图像数据传输对象
/// </summary>
public class ImageDto
{
    /// <summary>
    /// 图像ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 图像名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 图像格式
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// 图像数据（Base64编码）
    /// </summary>
    public string? DataBase64 { get; set; }

    /// <summary>
    /// 图像URL（如果存储在服务器）
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 上传图像请求
/// </summary>
public class UploadImageRequest
{
    /// <summary>
    /// 图像名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图像数据（Base64编码）
    /// </summary>
    public string DataBase64 { get; set; } = string.Empty;
}
