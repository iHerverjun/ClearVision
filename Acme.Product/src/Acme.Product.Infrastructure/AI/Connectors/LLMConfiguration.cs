// LLMConfiguration.cs
// LLM 配置定义
// 定义模型提供方、密钥与连接参数结构
// 作者：蘅芜君
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI.Connectors;

public enum LLMProviderType
{
    OpenAI,
    AzureOpenAI,
    Ollama
}

public class LLMConfiguration
{
    public string ProfileName { get; set; } = string.Empty;
    public LLMProviderType Provider { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
}

public class LLMConfigurationList
{
    public string ActiveProfile { get; set; } = string.Empty;
    public List<LLMConfiguration> Profiles { get; set; } = new();
}

public interface ILLMConfigurationStore
{
    Task<LLMConfiguration?> LoadAsync(string profileName);
    Task<LLMConfiguration?> GetActiveProfileAsync();
    Task SetActiveProfileAsync(string profileName);
    Task SaveAsync(LLMConfiguration config);
    Task<List<string>> ListProfilesAsync();
}
