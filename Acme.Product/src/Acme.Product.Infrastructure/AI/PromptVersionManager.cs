// PromptVersionManager.cs
// 提示词版本管理器
// 管理提示词模板版本、指标与兼容策略
// 作者：蘅芜君
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Acme.Product.Infrastructure.AI;

// 提示词使用指标
public class PromptMetrics
{
    public int TotalCalls { get; set; }
    public int SuccessCalls { get; set; }
    public int TotalTokenUsage { get; set; }
    public long TotalLatencyMs { get; set; }

    public double SuccessRate => TotalCalls == 0 ? 0 : (double)SuccessCalls / TotalCalls;
    public double AvgTokens => TotalCalls == 0 ? 0 : (double)TotalTokenUsage / TotalCalls;
    public double AvgLatencyMs => TotalCalls == 0 ? 0 : (double)TotalLatencyMs / TotalCalls;
}

// 提示词版本
public class PromptVersion
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public PromptMetrics Metrics { get; set; } = new();
}

public class PromptVersionList
{
    public Guid? ActiveVersionId { get; set; }
    public List<PromptVersion> Versions { get; set; } = new();
}

public interface IPromptVersionManager
{
    Task<PromptVersion> CreateVersionAsync(string name, string content, string description, string createdBy = "System");
    Task<PromptVersion?> GetVersionAsync(Guid id);
    Task<List<PromptVersion>> ListVersionsAsync();
    Task ActivateVersionAsync(Guid id);
    Task DeleteVersionAsync(Guid id);
    Task<PromptVersion> GetActiveVersionAsync();
    Task RecordMetricsAsync(Guid versionId, bool success, int tokenUsage, long latencyMs);
}

// 提示词版本管理器
public class PromptVersionManager : IPromptVersionManager
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public PromptVersionManager()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision");
        if (!Directory.Exists(appData))
            Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "prompt_versions.json");
    }

    private PromptVersionList LoadData()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return new PromptVersionList();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<PromptVersionList>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PromptVersionList();
            }
            catch
            {
                return new PromptVersionList();
            }
        }
    }

    private void SaveData(PromptVersionList data)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }

    public Task<PromptVersion> CreateVersionAsync(string name, string content, string description, string createdBy = "System")
    {
        var data = LoadData();
        var version = new PromptVersion
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Metrics = new PromptMetrics()
        };

        data.Versions.Add(version);

        // 如果是第一个版本，自动激活
        if (data.Versions.Count == 1)
        {
            data.ActiveVersionId = version.Id;
        }

        SaveData(data);
        return Task.FromResult(version);
    }

    public Task<PromptVersion?> GetVersionAsync(Guid id)
    {
        var data = LoadData();
        var version = data.Versions.FirstOrDefault(v => v.Id == id);
        return Task.FromResult(version);
    }

    public Task<List<PromptVersion>> ListVersionsAsync()
    {
        var data = LoadData();
        return Task.FromResult(data.Versions);
    }

    public Task ActivateVersionAsync(Guid id)
    {
        var data = LoadData();
        if (data.Versions.Any(v => v.Id == id))
        {
            data.ActiveVersionId = id;
            SaveData(data);
        }
        else
        {
            throw new ArgumentException("指定的提示词版本不存在。");
        }
        return Task.CompletedTask;
    }

    public Task DeleteVersionAsync(Guid id)
    {
        var data = LoadData();
        var version = data.Versions.FirstOrDefault(v => v.Id == id);
        if (version != null)
        {
            data.Versions.Remove(version);

            // 如果删除的是激活的版本，则重置激活状态
            if (data.ActiveVersionId == id)
            {
                data.ActiveVersionId = data.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.Id;
            }

            SaveData(data);
        }
        return Task.CompletedTask;
    }

    public async Task<PromptVersion> GetActiveVersionAsync()
    {
        var data = LoadData();
        if (data.ActiveVersionId.HasValue)
        {
            var activeVersion = data.Versions.FirstOrDefault(v => v.Id == data.ActiveVersionId.Value);
            if (activeVersion != null)
            {
                return activeVersion;
            }
        }

        // 如果没有任何激活的版本，则自动初始化一个默认的基线版本
        var defaultPrompt = @"You are a professional industrial vision inspection flow generation assistant for ClearVision platform.
Your task is to convert natural language requirements into structured JSON flow definitions.
Always respond with valid JSON that matches the ClearVision flow schema.";

        var newVersion = await CreateVersionAsync("V1.0 - Baseline", defaultPrompt, "系统初始默认提示词");
        await ActivateVersionAsync(newVersion.Id);
        return newVersion;
    }

    public Task RecordMetricsAsync(Guid versionId, bool success, int tokenUsage, long latencyMs)
    {
        var data = LoadData();
        var version = data.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version != null)
        {
            version.Metrics.TotalCalls++;
            if (success)
                version.Metrics.SuccessCalls++;
            version.Metrics.TotalTokenUsage += tokenUsage;
            version.Metrics.TotalLatencyMs += latencyMs;

            SaveData(data);
        }
        return Task.CompletedTask;
    }
}
