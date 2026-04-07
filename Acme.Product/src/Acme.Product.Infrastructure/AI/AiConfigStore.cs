// AiConfigStore.cs
// AI 配置存储
// 负责 AI 配置的读取、写入与默认值管理
// 作者：蘅芜君
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Runtime store for AI model profiles.
/// Persists models to ai_models.json and migrates old ai_config.json on first load.
/// </summary>
public class AiConfigStore
{
    private readonly Microsoft.Extensions.Logging.ILogger<AiConfigStore> _logger;
    private readonly object _lock = new();
    private List<AiModelConfig> _models;
    private readonly AiGenerationOptions _initialOptions;
    private readonly string _modelsFilePath;
    private readonly string _legacyConfigFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AiConfigStore(IOptions<AiGenerationOptions> initialOptions, Microsoft.Extensions.Logging.ILogger<AiConfigStore> logger)
        : this(initialOptions, logger, AppContext.BaseDirectory)
    {
    }

    public AiConfigStore(
        IOptions<AiGenerationOptions> initialOptions,
        Microsoft.Extensions.Logging.ILogger<AiConfigStore> logger,
        string storageDirectory)
    {
        _logger = logger;
        _initialOptions = CloneOptions(initialOptions.Value);
        if (string.IsNullOrWhiteSpace(storageDirectory))
            throw new ArgumentException("Storage directory must not be empty.", nameof(storageDirectory));

        Directory.CreateDirectory(storageDirectory);
        _modelsFilePath = Path.Combine(storageDirectory, "ai_models.json");
        _legacyConfigFilePath = Path.Combine(storageDirectory, "ai_config.json");
        _models = LoadOrMigrate(_initialOptions);
    }

    public List<AiModelConfig> GetAll()
    {
        lock (_lock)
        {
            return _models.Select(CloneModel).ToList();
        }
    }

    public AiModelConfig? GetById(string id)
    {
        lock (_lock)
        {
            var model = _models.FirstOrDefault(x => x.Id == id);
            return model == null ? null : CloneModel(model);
        }
    }

    /// <summary>
    /// Legacy compatibility helper. Returns active model as generation options.
    /// </summary>
    public AiGenerationOptions Get()
    {
        lock (_lock)
        {
            var active = _models.FirstOrDefault(x => x.IsActive) ?? _models.FirstOrDefault();
            if (active == null)
                throw new InvalidOperationException("没有可用的 AI 模型配置");

            return active.ToGenerationOptions();
        }
    }

    public AiModelConfig Add(AiModelConfig model)
    {
        lock (_lock)
        {
            if (model.Reasoning == null)
                model.NormalizeAdvancedFields();

            model.ValidateReasoningConfiguration();
            model.NormalizeAdvancedFields();
            model.Capabilities = (model.Capabilities?.Clone() ?? AiModelCapabilities.Infer(model.Provider, model.Model)).Normalize();

            if (_models.Count == 0)
                model.IsActive = true;

            _models.Add(model);
        }

        Save();
        _logger.LogInformation("[AiConfigStore] 新增模型: {Name} ({Id})", model.Name, model.Id);
        return model;
    }

    /// <summary>
    /// Update model by id. Empty/null ApiKey preserves existing key.
    /// </summary>
    public AiModelConfig? Update(string id, AiModelConfig updated)
    {
        lock (_lock)
        {
            var index = _models.FindIndex(x => x.Id == id);
            if (index < 0)
                return null;

            var candidate = CloneModel(_models[index]);
            ApplyUpdatedValues(candidate, updated);
            candidate.ValidateReasoningConfiguration();
            candidate.NormalizeAdvancedFields();
            _models[index] = candidate;
        }

        Save();
        _logger.LogInformation("[AiConfigStore] 更新模型: {Name} ({Id})", updated.Name ?? string.Empty, id);
        return GetById(id);
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (_models.Count <= 1)
                throw new InvalidOperationException("至少需保留一个模型配置");

            var removed = _models.RemoveAll(x => x.Id == id);
            if (removed == 0)
                return false;

            if (!_models.Any(x => x.IsActive) && _models.Count > 0)
            {
                _models[0].IsActive = true;
            }
        }

        Save();
        _logger.LogInformation("[AiConfigStore] 删除模型: {Id}", id);
        return true;
    }

    public bool SetActive(string id)
    {
        lock (_lock)
        {
            var target = _models.FirstOrDefault(x => x.Id == id);
            if (target == null)
                return false;

            foreach (var model in _models)
                model.IsActive = model.Id == id;
        }

        Save();
        _logger.LogInformation("[AiConfigStore] 激活模型切换为: {Id}", id);
        return true;
    }

    public List<AiModelConfig> ResetToDefaults()
    {
        List<AiModelConfig> resetModels;
        lock (_lock)
        {
            _models = CreateDefaultModels(_initialOptions);
            resetModels = _models.Select(CloneModel).ToList();
        }

        Save();

        try
        {
            if (File.Exists(_legacyConfigFilePath))
            {
                File.Delete(_legacyConfigFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiConfigStore] 删除旧版 ai_config.json 失败: {Message}", ex.Message);
        }

        _logger.LogInformation("[AiConfigStore] AI 模型配置已重置为默认值");
        return resetModels;
    }

    private List<AiModelConfig> LoadOrMigrate(AiGenerationOptions fallback)
    {
        if (File.Exists(_modelsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_modelsFilePath);
                var models = JsonSerializer.Deserialize<List<AiModelConfig>>(json, JsonOptions);
                if (models is { Count: > 0 })
                {
                    EnsureOneActive(models);
                    EnsureCapabilities(models);
                    EnsureAdvancedFields(models);
                    _logger.LogInformation("[AiConfigStore] 从 ai_models.json 加载 {Count} 个模型配置", models.Count);
                    return models;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiConfigStore] 读取 ai_models.json 失败: {Message}", ex.Message);
            }
        }

        if (File.Exists(_legacyConfigFilePath))
        {
            try
            {
                var json = File.ReadAllText(_legacyConfigFilePath);
                var legacy = JsonSerializer.Deserialize<AiGenerationOptions>(json, JsonOptions);
                if (legacy != null)
                {
                    _logger.LogInformation("[AiConfigStore] 从旧版 ai_config.json 迁移配置");
                    var migrated = new AiModelConfig
                    {
                        Id = "model_migrated",
                        Name = "系统默认模型",
                        Provider = legacy.Provider,
                        Protocol = AiModelConfig.NormalizeProtocol(null, legacy.Provider),
                        AuthMode = AiModelConfig.NormalizeAuthMode(null, AiModelConfig.NormalizeProtocol(null, legacy.Provider)),
                        ApiKey = legacy.ApiKey,
                        Model = legacy.Model,
                        BaseUrl = legacy.BaseUrl,
                        TimeoutMs = legacy.TimeoutSeconds * 1000,
                        RoleBindings = new List<string> { "generation" },
                        Priority = 100,
                        Capabilities = AiModelCapabilities.Infer(legacy.Provider, legacy.Model),
                        Reasoning = new AiReasoningSettings(),
                        IsActive = true
                    };
                    migrated.NormalizeAdvancedFields();

                    var models = new List<AiModelConfig> { migrated };
                    _models = models;
                    Save();
                    return models;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiConfigStore] 迁移旧配置失败: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("[AiConfigStore] 使用 appsettings.json 默认值初始化");
        var result = CreateDefaultModels(fallback);
        _models = result;
        Save();
        return result;
    }

    private static void EnsureOneActive(List<AiModelConfig> models)
    {
        if (!models.Any(x => x.IsActive) && models.Count > 0)
            models[0].IsActive = true;
    }

    private static void EnsureCapabilities(List<AiModelConfig> models)
    {
        foreach (var model in models)
        {
            model.Capabilities = (model.Capabilities?.Clone() ?? AiModelCapabilities.Infer(model.Provider, model.Model)).Normalize();
            model.Reasoning = (model.Reasoning?.Clone() ?? new AiReasoningSettings()).Normalize();
        }
    }

    private static void EnsureAdvancedFields(List<AiModelConfig> models)
    {
        foreach (var model in models)
        {
            model.NormalizeAdvancedFields();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_models, JsonOptions);
            File.WriteAllText(_modelsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiConfigStore] 持久化失败: {Message}", ex.Message);
        }
    }

    private static List<AiModelConfig> CreateDefaultModels(AiGenerationOptions fallback)
    {
        var defaultModel = new AiModelConfig
        {
            Id = "model_default",
            Name = "系统默认模型",
            Provider = fallback.Provider,
            Protocol = AiModelConfig.NormalizeProtocol(null, fallback.Provider),
            AuthMode = AiModelConfig.NormalizeAuthMode(null, AiModelConfig.NormalizeProtocol(null, fallback.Provider)),
            ApiKey = fallback.ApiKey,
            Model = fallback.Model,
            BaseUrl = fallback.BaseUrl,
            TimeoutMs = Math.Max(1, fallback.TimeoutSeconds) * 1000,
            RoleBindings = new List<string> { "generation" },
            Priority = 100,
            Capabilities = AiModelCapabilities.Infer(fallback.Provider, fallback.Model),
            Reasoning = new AiReasoningSettings(),
            IsActive = true
        };
        defaultModel.NormalizeAdvancedFields();

        return new List<AiModelConfig> { defaultModel };
    }

    private static AiModelConfig CloneModel(AiModelConfig model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Provider = model.Provider,
        ApiKey = model.ApiKey,
        Model = model.Model,
        BaseUrl = model.BaseUrl,
        TimeoutMs = model.TimeoutMs,
        Protocol = model.Protocol,
        AuthMode = model.AuthMode,
        AuthHeaderName = model.AuthHeaderName,
        ExtraHeaders = CloneStringDictionary(model.ExtraHeaders),
        ExtraQuery = CloneStringDictionary(model.ExtraQuery),
        ExtraBody = CloneJsonDictionary(model.ExtraBody),
        RoleBindings = model.RoleBindings == null ? null : new List<string>(model.RoleBindings),
        Priority = model.Priority,
        Capabilities = model.Capabilities?.Clone(),
        Reasoning = model.Reasoning?.Clone(),
        IsActive = model.IsActive
    };

    private static void ApplyUpdatedValues(AiModelConfig candidate, AiModelConfig updated)
    {
        var providerChanged = !string.IsNullOrWhiteSpace(updated.Provider) &&
            !string.Equals(updated.Provider, candidate.Provider, StringComparison.Ordinal);

        candidate.Name = updated.Name ?? candidate.Name;
        candidate.Provider = updated.Provider ?? candidate.Provider;
        candidate.Model = updated.Model ?? candidate.Model;
        candidate.BaseUrl = updated.BaseUrl;
        candidate.TimeoutMs = updated.TimeoutMs > 0 ? updated.TimeoutMs : candidate.TimeoutMs;
        candidate.Protocol = updated.Protocol ?? (providerChanged ? null : candidate.Protocol);
        candidate.AuthMode = updated.AuthMode ?? (providerChanged || updated.Protocol != null ? null : candidate.AuthMode);
        candidate.AuthHeaderName = updated.AuthHeaderName ??
            (providerChanged || updated.Protocol != null || updated.AuthMode != null ? null : candidate.AuthHeaderName);
        candidate.Priority = updated.Priority ?? candidate.Priority;

        if (updated.ExtraHeaders != null)
            candidate.ExtraHeaders = CloneStringDictionary(updated.ExtraHeaders);

        if (updated.ExtraQuery != null)
            candidate.ExtraQuery = CloneStringDictionary(updated.ExtraQuery);

        if (updated.ExtraBody != null)
            candidate.ExtraBody = CloneJsonDictionary(updated.ExtraBody);

        if (updated.RoleBindings != null)
            candidate.RoleBindings = new List<string>(updated.RoleBindings);

        if (updated.Capabilities != null)
        {
            candidate.Capabilities = updated.Capabilities.Clone().Normalize();
        }

        if (updated.Reasoning != null)
        {
            candidate.Reasoning = updated.Reasoning.Clone().Normalize();
        }

        if (!string.IsNullOrEmpty(updated.ApiKey))
        {
            candidate.ApiKey = updated.ApiKey;
        }
    }

    private static Dictionary<string, string>? CloneStringDictionary(Dictionary<string, string>? source)
    {
        if (source == null || source.Count == 0)
            return null;

        return source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, JsonElement>? CloneJsonDictionary(Dictionary<string, JsonElement>? source)
    {
        if (source == null || source.Count == 0)
            return null;

        var cloned = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source)
        {
            cloned[kv.Key] = kv.Value.Clone();
        }

        return cloned;
    }

    private static AiGenerationOptions CloneOptions(AiGenerationOptions options) => new()
    {
        Provider = options.Provider,
        ApiKey = options.ApiKey,
        Model = options.Model,
        MaxRetries = options.MaxRetries,
        TimeoutSeconds = options.TimeoutSeconds,
        MaxTokens = options.MaxTokens,
        Temperature = options.Temperature,
        BaseUrl = options.BaseUrl,
        Protocol = options.Protocol,
        AuthMode = options.AuthMode,
        AuthHeaderName = options.AuthHeaderName,
        ExtraHeaders = CloneStringDictionary(options.ExtraHeaders),
        ExtraQuery = CloneStringDictionary(options.ExtraQuery),
        ExtraBody = CloneJsonDictionary(options.ExtraBody),
        ReasoningMode = options.ReasoningMode,
        ReasoningEffort = options.ReasoningEffort
    };
}
