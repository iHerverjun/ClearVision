// AiModelCapabilities.cs
// AI 模型能力定义
// 描述模型支持的功能范围与限制项
// 作者：蘅芜君
namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Declares runtime capabilities for a model profile.
/// Stage C uses this object as the decision source for routing and fallback.
/// </summary>
public class AiModelCapabilities
{
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsVisionInput { get; set; } = true;
    public bool SupportsReasoningStream { get; set; }
    public bool SupportsJsonMode { get; set; } = true;
    public bool SupportsToolCall { get; set; }
    public bool SupportsSystemPrompt { get; set; } = true;
    public int MaxImageCount { get; set; } = 4;
    public int MaxImageBytes { get; set; } = 20 * 1024 * 1024;

    public AiModelCapabilities Clone()
    {
        return new AiModelCapabilities
        {
            SupportsStreaming = SupportsStreaming,
            SupportsVisionInput = SupportsVisionInput,
            SupportsReasoningStream = SupportsReasoningStream,
            SupportsJsonMode = SupportsJsonMode,
            SupportsToolCall = SupportsToolCall,
            SupportsSystemPrompt = SupportsSystemPrompt,
            MaxImageCount = MaxImageCount,
            MaxImageBytes = MaxImageBytes
        };
    }

    public AiModelCapabilities Normalize()
    {
        if (MaxImageCount < 0)
            MaxImageCount = 0;

        if (MaxImageBytes <= 0)
            MaxImageBytes = 20 * 1024 * 1024;

        if (!SupportsVisionInput)
            MaxImageCount = 0;

        return this;
    }

    public static AiModelCapabilities Infer(string? provider, string? model)
    {
        var caps = new AiModelCapabilities();

        var providerKey = (provider ?? string.Empty).ToLowerInvariant();
        var modelKey = (model ?? string.Empty).ToLowerInvariant();

        if (providerKey.Contains("anthropic"))
        {
            // Anthropic response streaming is mature and commonly includes thinking deltas.
            caps.SupportsReasoningStream = true;
        }

        if (modelKey.Contains("reasoner", StringComparison.OrdinalIgnoreCase))
        {
            // Current known pattern: reasoner class models are text-only for this pipeline.
            caps.SupportsVisionInput = false;
            caps.SupportsReasoningStream = true;
            caps.MaxImageCount = 0;
        }

        return caps.Normalize();
    }
}
