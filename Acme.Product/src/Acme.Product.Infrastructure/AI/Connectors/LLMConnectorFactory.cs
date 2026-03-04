// LLMConnectorFactory.cs
// LLM 连接器工厂
// 根据配置创建并返回对应模型连接器实例
// 作者：蘅芜君
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.AI.Connectors;

public class LLMConnectorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    public LLMConnectorFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public ILLMConnector Create(LLMConfiguration config)
    {
        return Create(config.Provider, config.Settings);
    }

    public ILLMConnector Create(LLMProviderType type, Dictionary<string, string> settings)
    {
        return type switch
        {
            LLMProviderType.OpenAI => CreateOpenAIConnector(settings),
            LLMProviderType.AzureOpenAI => CreateAzureOpenAIConnector(settings),
            LLMProviderType.Ollama => CreateOllamaConnector(settings),
            _ => throw new NotSupportedException($"不支持的 LLM 提供商: {type}")
        };
    }

    private ILLMConnector CreateOpenAIConnector(Dictionary<string, string> settings)
    {
        var config = new OpenAiConfig
        {
            ApiKey = GetString(settings, "ApiKey", ""),
            Model = GetString(settings, "Model", "gpt-4"),
            BaseUrl = GetString(settings, "BaseUrl", "https://api.openai.com/v1"),
            Temperature = GetFloat(settings, "Temperature", 0.1f),
            MaxTokens = GetInt(settings, "MaxTokens", 4000)
        };

        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("LLM");
        return new OpenAiConnector(config, httpClient);
    }

    private ILLMConnector CreateAzureOpenAIConnector(Dictionary<string, string> settings)
    {
        var config = new AzureOpenAiConfig
        {
            Endpoint = GetString(settings, "Endpoint", ""),
            DeploymentName = GetString(settings, "DeploymentName", ""),
            ApiKey = GetString(settings, "ApiKey", ""),
            AccessToken = GetString(settings, "AccessToken", ""),
            ApiVersion = GetString(settings, "ApiVersion", "2024-02-15-preview"),
            Temperature = GetFloat(settings, "Temperature", 0.1f),
            MaxTokens = GetInt(settings, "MaxTokens", 4000)
        };

        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("LLM");
        return new AzureOpenAiConnector(config, httpClient);
    }

    private ILLMConnector CreateOllamaConnector(Dictionary<string, string> settings)
    {
        var config = new OllamaConfig
        {
            BaseUrl = GetString(settings, "BaseUrl", "http://localhost:11434"),
            Model = GetString(settings, "Model", "llama3"),
            Temperature = GetFloat(settings, "Temperature", 0.1f),
            ContextWindow = GetInt(settings, "ContextWindow", 4096)
        };

        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("LLM");
        return new OllamaConnector(config, httpClient);
    }

    private string GetString(Dictionary<string, string> settings, string key, string defaultValue)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
    }

    private int GetInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        if (settings.TryGetValue(key, out var stringValue) && int.TryParse(stringValue, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    private float GetFloat(Dictionary<string, string> settings, string key, float defaultValue)
    {
        if (settings.TryGetValue(key, out var stringValue) && float.TryParse(stringValue, out var value))
        {
            return value;
        }
        return defaultValue;
    }
}
