// AiConfigStore.cs
// AI 配置运行时管理（线程安全单例）
// 作者：蘅芜君

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 配置的运行时存储。
/// 启动时从 ai_config.json 加载（如不存在则从 appsettings.json 迁移），
/// 更新后自动持久化。
/// </summary>
public class AiConfigStore
{
    private readonly ILogger<AiConfigStore> _logger;
    private readonly object _lock = new();
    private AiGenerationOptions _current;
    private readonly string _configFilePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AiConfigStore(IOptions<AiGenerationOptions> initialOptions, ILogger<AiConfigStore> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "ai_config.json");

        // 尝试从独立配置文件加载
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = File.ReadAllText(_configFilePath);
                _current = JsonSerializer.Deserialize<AiGenerationOptions>(json, _jsonOptions)
                           ?? initialOptions.Value;
                _logger.LogInformation("[AiConfigStore] 从 ai_config.json 加载配置成功, Provider={Provider}, Model={Model}",
                    _current.Provider, _current.Model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiConfigStore] 读取 ai_config.json 失败，使用 appsettings.json 配置。错误：{Message}", ex.Message);
                _current = initialOptions.Value;
            }
        }
        else
        {
            // 首次运行：从 appsettings.json 迁移
            _current = initialOptions.Value;
            _logger.LogInformation("[AiConfigStore] ai_config.json 不存在，从 appsettings.json 迁移初始配置");
            try
            {
                Save();
                _logger.LogInformation("[AiConfigStore] 初始配置已保存到 ai_config.json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiConfigStore] 保存初始 ai_config.json 失败。错误：{Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 获取当前 AI 配置的快照
    /// </summary>
    public AiGenerationOptions Get()
    {
        lock (_lock)
        {
            return new AiGenerationOptions
            {
                Provider = _current.Provider,
                ApiKey = _current.ApiKey,
                Model = _current.Model,
                MaxRetries = _current.MaxRetries,
                TimeoutSeconds = _current.TimeoutSeconds,
                MaxTokens = _current.MaxTokens,
                Temperature = _current.Temperature,
                BaseUrl = _current.BaseUrl
            };
        }
    }

    /// <summary>
    /// 更新 AI 配置并持久化
    /// </summary>
    public void Update(AiGenerationOptions newOptions)
    {
        lock (_lock)
        {
            _current = newOptions;
        }

        Save();
        _logger.LogInformation("[AiConfigStore] AI 配置已更新, Provider={Provider}, Model={Model}, BaseUrl={BaseUrl}",
            newOptions.Provider, newOptions.Model, newOptions.BaseUrl ?? "(default)");
    }

    /// <summary>
    /// 获取脱敏后的配置（用于前端展示）
    /// </summary>
    public object GetMasked()
    {
        var options = Get();
        return new
        {
            provider = options.Provider,
            apiKey = MaskApiKey(options.ApiKey),
            model = options.Model,
            baseUrl = options.BaseUrl ?? "",
            maxRetries = options.MaxRetries,
            timeoutSeconds = options.TimeoutSeconds,
            maxTokens = options.MaxTokens,
            temperature = options.Temperature
        };
    }

    private static string MaskApiKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "";
        if (key.Length <= 8)
            return "****";
        return key[..4] + new string('*', key.Length - 8) + key[^4..];
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_current, _jsonOptions);
        File.WriteAllText(_configFilePath, json);
    }
}
