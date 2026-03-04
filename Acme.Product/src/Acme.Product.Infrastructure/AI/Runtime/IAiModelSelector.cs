// IAiModelSelector.cs
// AI 模型选择器接口
// 定义按请求上下文选择目标模型的策略契约
// 作者：蘅芜君
namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Selects model profiles for different runtime intents.
/// Stage A keeps selection strategy simple: active model only.
/// </summary>
public interface IAiModelSelector
{
    AiModelConfig SelectGenerationModel();
}
