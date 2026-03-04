// AIGeneratedFlowVersionManager.cs
// AI 生成流程版本管理器
// 管理生成流程版本、回滚与版本元数据
// 作者：蘅芜君
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Acme.Product.Core.Entities;

namespace Acme.Product.Infrastructure.AI;

public class PromptVersionInfo
{
    public Guid VersionId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AIGeneratedFlowVersion
{
    public Guid Id { get; set; }
    public Guid FlowId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserRequirement { get; set; } = string.Empty;
    public OperatorFlow Flow { get; set; } = new();
    public PromptVersionInfo UsedPrompt { get; set; } = new();
    public string UsedProvider { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public WorkflowTelemetry Telemetry { get; set; } = new();
    public bool IsDeployed { get; set; }
    public DateTime? DeployedAt { get; set; }
}

public class FlowVersionList
{
    public List<AIGeneratedFlowVersion> Versions { get; set; } = new();
}

public interface IAIGeneratedFlowVersionManager
{
    Task<AIGeneratedFlowVersion> SaveVersionAsync(OperatorFlow flow, string userRequirement, PromptVersionInfo promptInfo, string provider, WorkflowTelemetry telemetry, string createdBy = "System");
    Task<AIGeneratedFlowVersion?> GetVersionAsync(Guid versionId);
    Task<List<AIGeneratedFlowVersion>> GetFlowHistoryAsync(Guid flowId);
    Task MarkAsDeployedAsync(Guid versionId);
}

public class AIGeneratedFlowVersionManager : IAIGeneratedFlowVersionManager
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public AIGeneratedFlowVersionManager()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision");
        if (!Directory.Exists(appData))
            Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "ai_flow_versions.json");
    }

    private FlowVersionList LoadData()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return new FlowVersionList();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<FlowVersionList>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FlowVersionList();
            }
            catch
            {
                return new FlowVersionList();
            }
        }
    }

    private void SaveData(FlowVersionList data)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }

    public Task<AIGeneratedFlowVersion> SaveVersionAsync(
        OperatorFlow flow,
        string userRequirement,
        PromptVersionInfo promptInfo,
        string provider,
        WorkflowTelemetry telemetry,
        string createdBy = "System")
    {
        var data = LoadData();

        var flowId = flow.Id;
        // 自动计算版本号：取相同 FlowId 的历史最大版本号 + 1
        var history = data.Versions.Where(v => v.FlowId == flowId).ToList();
        int nextVersion = history.Count > 0 ? history.Max(v => v.VersionNumber) + 1 : 1;

        var newVersion = new AIGeneratedFlowVersion
        {
            Id = Guid.NewGuid(),
            FlowId = flowId,
            FlowName = flow.Name,
            VersionNumber = nextVersion,
            VersionName = $"V{nextVersion}.0",
            Description = $"自动生成版本 V{nextVersion}.0",
            UserRequirement = userRequirement,
            Flow = flow,
            UsedPrompt = promptInfo,
            UsedProvider = provider,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = createdBy,
            Telemetry = telemetry,
            IsDeployed = false
        };

        data.Versions.Add(newVersion);
        SaveData(data);

        return Task.FromResult(newVersion);
    }

    public Task<AIGeneratedFlowVersion?> GetVersionAsync(Guid versionId)
    {
        var data = LoadData();
        var version = data.Versions.FirstOrDefault(v => v.Id == versionId);
        return Task.FromResult(version);
    }

    public Task<List<AIGeneratedFlowVersion>> GetFlowHistoryAsync(Guid flowId)
    {
        var data = LoadData();
        var history = data.Versions
            .Where(v => v.FlowId == flowId)
            .OrderByDescending(v => v.VersionNumber)
            .ToList();
        return Task.FromResult(history);
    }

    public Task MarkAsDeployedAsync(Guid versionId)
    {
        var data = LoadData();
        var version = data.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version != null)
        {
            version.IsDeployed = true;
            version.DeployedAt = DateTime.UtcNow;

            // 可以选择将该流程先前的已部署版本标为未部署，以保证同一时间只有一个生效版本
            var others = data.Versions.Where(v => v.FlowId == version.FlowId && v.Id != versionId && v.IsDeployed);
            foreach (var other in others)
            {
                other.IsDeployed = false;
            }

            SaveData(data);
        }
        return Task.CompletedTask;
    }
}
