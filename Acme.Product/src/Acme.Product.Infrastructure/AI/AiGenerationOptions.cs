// AiGenerationOptions.cs
// AI 生成参数选项
// 定义模型参数、温度与输出约束配置
// 作者：蘅芜君
namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 工作流生成功能的配置选项
/// </summary>
public class AiGenerationOptions
{
    public const string SectionName = "AiFlowGeneration";

    /// <summary>
    /// AI 提供商：Anthropic 或 OpenAI
    /// </summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// AI API Key（生产环境从环境变量或密钥管理器读取）
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 使用的模型名称
    /// Anthropic 推荐：claude-opus-4-6
    /// OpenAI 推荐：gpt-4o
    /// </summary>
    public string Model { get; set; } = "claude-opus-4-6";

    /// <summary>
    /// 校验失败后的最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// API 调用超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 生成结果的最大 token 数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 温度系数 (0.0 - 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 自定义 API 端点地址（可选）。
    /// 用于连接 Ollama 本地模型（如 http://localhost:11434/v1/chat/completions）
    /// 或兼容 OpenAI 协议的国内 API 代理服务（如 DeepSeek、通义千问）。
    /// 为 null 时使用各提供商的默认端点。
    /// </summary>
    public string? BaseUrl { get; set; }
}
