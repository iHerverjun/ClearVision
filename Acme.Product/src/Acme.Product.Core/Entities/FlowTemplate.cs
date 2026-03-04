// FlowTemplate.cs
// 流程模板实体
// 定义流程模板的标识、内容与版本信息
// 作者：蘅芜君
namespace Acme.Product.Core.Entities;

/// <summary>
/// 工作流模板定义
/// </summary>
public class FlowTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string FlowJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
