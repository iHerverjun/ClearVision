// JsonConfigurationService.cs
// 基于 JSON 文件的配置服务实现
// 作者：蘅芜君

using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0005 // 忽略未使用的 using 指令警告

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 基于 JSON 文件的配置服务实现
/// </summary>
public class JsonConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly ILogger<JsonConfigurationService> _logger;
    private AppConfig _cachedConfig;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    public JsonConfigurationService(ILogger<JsonConfigurationService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        _cachedConfig = new AppConfig(); // 默认值
    }
    
    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
                _logger.LogInformation("配置已加载: {Path}", _configPath);
            }
            else
            {
                _logger.LogWarning("配置文件不存在，使用默认值: {Path}", _configPath);
                _cachedConfig = new AppConfig();
                await SaveAsync(_cachedConfig); // 创建默认配置文件
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置失败，使用默认值");
            _cachedConfig = new AppConfig();
        }
        
        return _cachedConfig;
    }
    
    public async Task SaveAsync(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
            _cachedConfig = config;
            _logger.LogInformation("配置已保存: {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
            throw;
        }
    }
    
    public AppConfig GetCurrent() => _cachedConfig;
}
