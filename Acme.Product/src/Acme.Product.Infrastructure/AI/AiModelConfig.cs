// AiModelConfig.cs
// AI 模型配置实体（支持多模型数组存储）
// 作者：蘅芜君

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 单个 AI 模型的完整配置
/// </summary>
public class AiModelConfig
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = $"model_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    /// <summary>显示名称（如"DeepSeek"、"Kimi"）</summary>
    public string Name { get; set; } = "新建模型";

    /// <summary>API 协议："OpenAI" / "OpenAI Compatible" / "Anthropic"</summary>
    public string Provider { get; set; } = "OpenAI Compatible";

    /// <summary>真实 API 密钥（GET 接口不返回此字段）</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型标识（如 deepseek-reasoner, gpt-4o）</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>自定义 API 端点地址（可选）</summary>
    public string? BaseUrl { get; set; }

    /// <summary>请求超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 120000;

    /// <summary>是否为当前激活模型</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 转换为 AiGenerationOptions（供 AiApiClient 消费）
    /// </summary>
    public AiGenerationOptions ToGenerationOptions()
    {
        return new AiGenerationOptions
        {
            Provider = Provider,
            ApiKey = ApiKey,
            Model = Model,
            BaseUrl = BaseUrl,
            TimeoutSeconds = TimeoutMs / 1000,
            MaxRetries = 2,
            MaxTokens = 4096,
            Temperature = 0.7
        };
    }
}
