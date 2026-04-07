using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

public class JsonConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly ILogger<JsonConfigurationService> _logger;
    private AppConfig _cachedConfig;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonConfigurationService(ILogger<JsonConfigurationService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        _cachedConfig = new AppConfig();
        _cachedConfig.Normalize();
    }

    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                _cachedConfig.Normalize();
                _logger.LogInformation("Configuration loaded from {Path}", _configPath);
            }
            else
            {
                _logger.LogWarning("Configuration file not found. Creating defaults at {Path}", _configPath);
                _cachedConfig = new AppConfig();
                _cachedConfig.Normalize();
                await SaveAsync(_cachedConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration, using defaults.");
            _cachedConfig = new AppConfig();
            _cachedConfig.Normalize();
        }

        return _cachedConfig;
    }

    public async Task SaveAsync(AppConfig config)
    {
        try
        {
            config ??= new AppConfig();
            config.Normalize();

            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
            _cachedConfig = config;
            _logger.LogInformation("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration.");
            throw;
        }
    }

    public AppConfig GetCurrent() => _cachedConfig;
}
