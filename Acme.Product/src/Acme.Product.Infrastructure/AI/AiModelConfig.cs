using System.Text.Json;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 单个 AI 模型的完整配置
/// </summary>
public class AiModelConfig
{
    public const string ProtocolOpenAiCompatible = "openai_compatible";
    public const string ProtocolAnthropic = "anthropic";
    public const string ProtocolAzureOpenAi = "azure_openai";
    public const string ProtocolOllamaNative = "ollama_native";

    public const string AuthModeBearer = "bearer";
    public const string AuthModeHeaderKey = "header_key";
    public const string AuthModeNone = "none";

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

    /// <summary>模型能力声明（可选，未配置时按 Provider/Model 推导）</summary>
    public AiModelCapabilities? Capabilities { get; set; }

    /// <summary>Protocol identity for provider routing.</summary>
    public string? Protocol { get; set; }

    /// <summary>Authentication mode: bearer | header_key | none.</summary>
    public string? AuthMode { get; set; }

    /// <summary>Header name when auth mode requires explicit key header.</summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>Additional HTTP headers to send.</summary>
    public Dictionary<string, string>? ExtraHeaders { get; set; }

    /// <summary>Additional query parameters to append.</summary>
    public Dictionary<string, string>? ExtraQuery { get; set; }

    /// <summary>Additional JSON body fields.</summary>
    public Dictionary<string, JsonElement>? ExtraBody { get; set; }

    /// <summary>Logical role bindings for selector policies.</summary>
    public List<string>? RoleBindings { get; set; }

    /// <summary>Priority used by fallback ordering. Smaller number = higher priority.</summary>
    public int? Priority { get; set; }

    public AiModelCapabilities GetEffectiveCapabilities()
    {
        return (Capabilities?.Clone() ?? AiModelCapabilities.Infer(Provider, Model)).Normalize();
    }

    public void NormalizeAdvancedFields()
    {
        var protocol = NormalizeProtocol(Protocol, Provider);
        Protocol = protocol;

        if (string.IsNullOrWhiteSpace(Provider))
            Provider = GetLegacyProviderByProtocol(protocol);

        AuthMode = NormalizeAuthMode(AuthMode, protocol);
        AuthHeaderName = NormalizeAuthHeaderName(AuthHeaderName, AuthMode!);
        RoleBindings = NormalizeRoleBindings(RoleBindings);
        Priority ??= 100;

        ExtraHeaders = NormalizeStringMap(ExtraHeaders);
        ExtraQuery = NormalizeStringMap(ExtraQuery);
        ExtraBody = NormalizeJsonMap(ExtraBody);
    }

    public static string NormalizeProtocol(string? protocol, string? provider)
    {
        if (!string.IsNullOrWhiteSpace(protocol))
        {
            var normalized = protocol.Trim().ToLowerInvariant();
            return normalized switch
            {
                ProtocolAnthropic => ProtocolAnthropic,
                ProtocolAzureOpenAi => ProtocolAzureOpenAi,
                ProtocolOllamaNative => ProtocolOllamaNative,
                _ => ProtocolOpenAiCompatible
            };
        }

        var providerKey = (provider ?? string.Empty).ToLowerInvariant();
        if (providerKey.Contains("anthropic"))
            return ProtocolAnthropic;
        if (providerKey.Contains("azure"))
            return ProtocolAzureOpenAi;
        if (providerKey.Contains("ollama"))
            return ProtocolOllamaNative;
        return ProtocolOpenAiCompatible;
    }

    public static string GetLegacyProviderByProtocol(string? protocol)
    {
        var normalized = NormalizeProtocol(protocol, null);
        return normalized switch
        {
            ProtocolAnthropic => "Anthropic Claude",
            ProtocolAzureOpenAi => "OpenAI API",
            ProtocolOllamaNative => "OpenAI Compatible",
            _ => "OpenAI Compatible"
        };
    }

    public static string NormalizeAuthMode(string? authMode, string protocol)
    {
        if (!string.IsNullOrWhiteSpace(authMode))
        {
            var normalized = authMode.Trim().ToLowerInvariant();
            return normalized switch
            {
                AuthModeNone => AuthModeNone,
                AuthModeHeaderKey => AuthModeHeaderKey,
                _ => AuthModeBearer
            };
        }

        var normalizedProtocol = NormalizeProtocol(protocol, null);
        return normalizedProtocol switch
        {
            ProtocolOllamaNative => AuthModeNone,
            ProtocolAnthropic => AuthModeHeaderKey,
            _ => AuthModeBearer
        };
    }

    public static string NormalizeAuthHeaderName(string? authHeaderName, string authMode)
    {
        var normalizedAuthMode = NormalizeAuthMode(authMode, ProtocolOpenAiCompatible);
        if (normalizedAuthMode == AuthModeNone)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(authHeaderName))
            return authHeaderName.Trim();

        return normalizedAuthMode == AuthModeHeaderKey ? "x-api-key" : "Authorization";
    }

    private static List<string> NormalizeRoleBindings(IEnumerable<string>? roleBindings)
    {
        if (roleBindings == null)
            return new List<string> { "generation" };

        var normalized = roleBindings
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x is "generation" or "reasoning" or "fallback" or "validation")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            normalized.Add("generation");

        return normalized;
    }

    private static Dictionary<string, string>? NormalizeStringMap(Dictionary<string, string>? map)
    {
        if (map == null || map.Count == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            result[kv.Key.Trim()] = kv.Value?.Trim() ?? string.Empty;
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, JsonElement>? NormalizeJsonMap(Dictionary<string, JsonElement>? map)
    {
        if (map == null || map.Count == 0)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            result[kv.Key.Trim()] = kv.Value.Clone();
        }

        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// 转换为 AiGenerationOptions（供 AiApiClient 消费）
    /// </summary>
    public AiGenerationOptions ToGenerationOptions()
    {
        var protocol = NormalizeProtocol(Protocol, Provider);
        var provider = string.IsNullOrWhiteSpace(Provider)
            ? GetLegacyProviderByProtocol(protocol)
            : Provider;

        return new AiGenerationOptions
        {
            Provider = provider,
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
