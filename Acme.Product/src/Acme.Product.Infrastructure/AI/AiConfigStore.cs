// AiConfigStore.cs
// AI 配置运行时管理（多模型数组存储，线程安全单例）
// 作者：蘅芜君

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 模型配置的运行时存储。
/// 支持多模型数组，持久化到 ai_models.json。
/// 首次启动时自动从旧版 ai_config.json（单配置）迁移。
/// </summary>
public class AiConfigStore
{
    private readonly Microsoft.Extensions.Logging.ILogger<AiConfigStore> _logger;
    private readonly object _lock = new();
    private List<AiModelConfig> _models;
    private readonly string _modelsFilePath;
    private readonly string _legacyConfigFilePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AiConfigStore(IOptions<AiGenerationOptions> initialOptions, Microsoft.Extensions.Logging.ILogger<AiConfigStore> logger)
    {
        _logger = logger;
        _modelsFilePath = Path.Combine(AppContext.BaseDirectory, "ai_models.json");
        _legacyConfigFilePath = Path.Combine(AppContext.BaseDirectory, "ai_config.json");

        _models = LoadOrMigrate(initialOptions.Value);
    }

    // ==================== 多模型 CRUD ====================

    /// <summary>获取所有模型配置</summary>
    public List<AiModelConfig> GetAll()
    {
        lock (_lock)
        {
            return _models.Select(CloneModel).ToList();
        }
    }

    /// <summary>根据 ID 获取指定模型</summary>
    public AiModelConfig? GetById(string id)
    {
        lock (_lock)
        {
            var m = _models.FirstOrDefault(x => x.Id == id);
            return m != null ? CloneModel(m) : null;
        }
    }

    /// <summary>获取当前激活模型的配置（兼容老接口）</summary>
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

    /// <summary>添加新模型</summary>
    public AiModelConfig Add(AiModelConfig model)
    {
        lock (_lock)
        {
            // 如果是第一个模型，自动激活
            if (_models.Count == 0)
                model.IsActive = true;

            _models.Add(model);
        }
        Save();
        _logger.LogInformation("[AiConfigStore] 新增模型: {Name} ({Id})", model.Name, model.Id);
        return model;
    }

    /// <summary>
    /// 更新指定模型。ApiKey 为 null 或空时保留原值。
    /// </summary>
    public AiModelConfig? Update(string id, AiModelConfig updated)
    {
        lock (_lock)
        {
            var existing = _models.FirstOrDefault(x => x.Id == id);
            if (existing == null)
                return null;

            existing.Name = updated.Name ?? existing.Name;
            existing.Provider = updated.Provider ?? existing.Provider;
            existing.Model = updated.Model ?? existing.Model;
            existing.BaseUrl = updated.BaseUrl; // 允许清空
            existing.TimeoutMs = updated.TimeoutMs > 0 ? updated.TimeoutMs : existing.TimeoutMs;

            // ApiKey 为空/null 时保留原值（用户没修改密钥）
            if (!string.IsNullOrEmpty(updated.ApiKey))
            {
                existing.ApiKey = updated.ApiKey;
            }
        }
        Save();
        _logger.LogInformation("[AiConfigStore] 更新模型: {Name} ({Id})", updated.Name ?? "", id);
        return GetById(id);
    }

    /// <summary>删除指定模型（不允许删除最后一个）</summary>
    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (_models.Count <= 1)
                throw new InvalidOperationException("至少需保留一个模型配置");

            var removed = _models.RemoveAll(x => x.Id == id);
            if (removed == 0)
                return false;

            // 如果删除的是激活模型，自动激活第一个
            if (!_models.Any(x => x.IsActive) && _models.Count > 0)
            {
                _models[0].IsActive = true;
            }
        }
        Save();
        _logger.LogInformation("[AiConfigStore] 删除模型: {Id}", id);
        return true;
    }

    /// <summary>设置指定模型为激活状态</summary>
    public bool SetActive(string id)
    {
        lock (_lock)
        {
            var target = _models.FirstOrDefault(x => x.Id == id);
            if (target == null)
                return false;

            foreach (var m in _models)
                m.IsActive = m.Id == id;
        }
        Save();
        _logger.LogInformation("[AiConfigStore] 激活模型切换为: {Id}", id);
        return true;
    }

    // ==================== 持久化与迁移 ====================

    private List<AiModelConfig> LoadOrMigrate(AiGenerationOptions fallback)
    {
        // 优先从新格式文件加载
        if (File.Exists(_modelsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_modelsFilePath);
                var models = JsonSerializer.Deserialize<List<AiModelConfig>>(json, _jsonOptions);
                if (models != null && models.Count > 0)
                {
                    _logger.LogInformation("[AiConfigStore] 从 ai_models.json 加载 {Count} 个模型配置", models.Count);
                    EnsureOneActive(models);
                    return models;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiConfigStore] 读取 ai_models.json 失败: {Message}", ex.Message);
            }
        }

        // 尝试从旧版单配置迁移
        if (File.Exists(_legacyConfigFilePath))
        {
            try
            {
                var json = File.ReadAllText(_legacyConfigFilePath);
                var legacy = JsonSerializer.Deserialize<AiGenerationOptions>(json, _jsonOptions);
                if (legacy != null)
                {
                    _logger.LogInformation("[AiConfigStore] 从旧版 ai_config.json 迁移配置");
                    var migrated = new AiModelConfig
                    {
                        Id = "model_migrated",
                        Name = "系统默认模型",
                        Provider = legacy.Provider,
                        ApiKey = legacy.ApiKey,
                        Model = legacy.Model,
                        BaseUrl = legacy.BaseUrl,
                        TimeoutMs = legacy.TimeoutSeconds * 1000,
                        IsActive = true
                    };
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

        // 全部失败：用 appsettings.json 默认值创建
        _logger.LogInformation("[AiConfigStore] 使用 appsettings.json 默认值初始化");
        var defaultModel = new AiModelConfig
        {
            Id = "model_default",
            Name = "系统默认模型",
            Provider = fallback.Provider,
            ApiKey = fallback.ApiKey,
            Model = fallback.Model,
            BaseUrl = fallback.BaseUrl,
            TimeoutMs = fallback.TimeoutSeconds * 1000,
            IsActive = true
        };
        var result = new List<AiModelConfig> { defaultModel };
        _models = result;
        Save();
        return result;
    }

    private static void EnsureOneActive(List<AiModelConfig> models)
    {
        if (!models.Any(x => x.IsActive) && models.Count > 0)
            models[0].IsActive = true;
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_models, _jsonOptions);
            File.WriteAllText(_modelsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiConfigStore] 持久化失败: {Message}", ex.Message);
        }
    }

    private static AiModelConfig CloneModel(AiModelConfig m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Provider = m.Provider,
        ApiKey = m.ApiKey,
        Model = m.Model,
        BaseUrl = m.BaseUrl,
        TimeoutMs = m.TimeoutMs,
        IsActive = m.IsActive
    };
}
