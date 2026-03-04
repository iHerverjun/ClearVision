// IAiModelRegistry.cs
// AI 模型注册表接口
// 定义模型元数据注册与查询能力
// 作者：蘅芜君
namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Read-only registry abstraction for model profiles used by the unified runtime pipeline.
/// </summary>
public interface IAiModelRegistry
{
    AiModelConfig GetActiveModel();
}
