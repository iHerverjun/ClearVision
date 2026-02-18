// ProjectDto.cs
// 算子流程（可选）
// 作者：蘅芜君

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 工程数据传输对象
/// </summary>
public class ProjectDto
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 工程名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工程描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 工程版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 算子流程
    /// </summary>
    public OperatorFlowDto? Flow { get; set; }

    /// <summary>
    /// 全局配置参数
    /// </summary>
    public Dictionary<string, string> GlobalSettings { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// 最后打开时间
    /// </summary>
    public DateTime? LastOpenedAt { get; set; }
}

/// <summary>
/// 创建工程请求
/// </summary>
public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// 算子流程（可选）
    /// </summary>
    public OperatorFlowDto? Flow { get; set; }
}

/// <summary>
/// 更新工程请求
/// </summary>
public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// 算子流程（可选）
    /// </summary>
    public OperatorFlowDto? Flow { get; set; }
}
