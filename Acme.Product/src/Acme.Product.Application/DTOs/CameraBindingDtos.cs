// CameraBindingDtos.cs
// 相机绑定 DTO 定义
// 定义相机绑定配置请求与响应的数据传输结构
// 作者：蘅芜君
using Acme.Product.Core.Entities;

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 更新相机绑定配置请求
/// </summary>
public class UpdateCameraBindingsRequest
{
    /// <summary>
    /// 相机绑定配置列表
    /// </summary>
    public List<CameraBindingConfig> Bindings { get; set; } = new();

    /// <summary>
    /// 活动相机ID
    /// </summary>
    public string ActiveCameraId { get; set; } = "";
}
