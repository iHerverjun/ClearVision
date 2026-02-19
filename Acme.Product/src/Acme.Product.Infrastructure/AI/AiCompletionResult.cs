namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI API 调用结果，包含内容和思维链推理过程
/// </summary>
public class AiCompletionResult
{
    /// <summary>
    /// AI 生成的主要内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// AI 的推理/思维链内容（DeepSeek reasoning_content / Anthropic thinking）
    /// 可能为空，取决于模型是否支持
    /// </summary>
    public string? Reasoning { get; set; }
}
