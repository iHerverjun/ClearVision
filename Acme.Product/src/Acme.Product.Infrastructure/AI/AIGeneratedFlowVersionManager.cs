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
    public List<ScenarioArtifactVersionRecord> ScenarioArtifactVersions { get; set; } = new();
}

public class ScenarioArtifactVersionRecord
{
    public Guid Id { get; set; }
    public string ScenarioKey { get; set; } = string.Empty;
    public ScenarioArtifactType ArtifactType { get; set; }
    public string ArtifactName { get; set; } = string.Empty;
    public string ArtifactVersion { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? ChecksumSha256 { get; set; }
    public Guid? SourceFlowVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public interface IAIGeneratedFlowVersionManager
{
    Task<AIGeneratedFlowVersion> SaveVersionAsync(OperatorFlow flow, string userRequirement, PromptVersionInfo promptInfo, string provider, WorkflowTelemetry telemetry, string createdBy = "System");
    Task<AIGeneratedFlowVersion?> GetVersionAsync(Guid versionId);
    Task<List<AIGeneratedFlowVersion>> GetFlowHistoryAsync(Guid flowId);
    Task MarkAsDeployedAsync(Guid versionId);
    Task<ScenarioArtifactVersionRecord> SaveScenarioArtifactVersionAsync(
        string scenarioKey,
        ScenarioArtifactType artifactType,
        string artifactName,
        string artifactVersion,
        string relativePath,
        Guid? sourceFlowVersionId = null,
        string? checksumSha256 = null,
        Dictionary<string, string>? metadata = null,
        string createdBy = "System");
    Task<List<ScenarioArtifactVersionRecord>> GetScenarioArtifactHistoryAsync(string scenarioKey, ScenarioArtifactType? artifactType = null);
    Task MarkScenarioArtifactActiveAsync(Guid artifactVersionId);
    Task<ScenarioPackageManifest?> BuildScenarioManifestAsync(
        string scenarioKey,
        string scenarioName,
        string description,
        string packageVersion,
        string createdBy = "System");
}

public class AIGeneratedFlowVersionManager : IAIGeneratedFlowVersionManager
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public AIGeneratedFlowVersionManager(string? baseDirectory = null)
    {
        var appData = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision")
            : baseDirectory;

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
                return JsonSerializer.Deserialize<FlowVersionList>(json, _jsonOptions) ?? new FlowVersionList();
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
            var json = JsonSerializer.Serialize(data, _jsonOptions);
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

    public Task<ScenarioArtifactVersionRecord> SaveScenarioArtifactVersionAsync(
        string scenarioKey,
        ScenarioArtifactType artifactType,
        string artifactName,
        string artifactVersion,
        string relativePath,
        Guid? sourceFlowVersionId = null,
        string? checksumSha256 = null,
        Dictionary<string, string>? metadata = null,
        string createdBy = "System")
    {
        if (string.IsNullOrWhiteSpace(scenarioKey))
            throw new ArgumentException("Scenario key is required.", nameof(scenarioKey));
        if (string.IsNullOrWhiteSpace(artifactName))
            throw new ArgumentException("Artifact name is required.", nameof(artifactName));
        if (string.IsNullOrWhiteSpace(artifactVersion))
            throw new ArgumentException("Artifact version is required.", nameof(artifactVersion));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Artifact relative path is required.", nameof(relativePath));

        var normalizedScenarioKey = scenarioKey.Trim();
        var normalizedArtifactName = artifactName.Trim();
        var normalizedArtifactVersion = artifactVersion.Trim();
        var normalizedRelativePath = NormalizeRelativePath(relativePath);

        var data = LoadData();
        var existingActive = data.ScenarioArtifactVersions
            .Where(item =>
                item.IsActive &&
                item.ScenarioKey.Equals(normalizedScenarioKey, StringComparison.OrdinalIgnoreCase) &&
                item.ArtifactType == artifactType &&
                item.ArtifactName.Equals(normalizedArtifactName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var activeVersion in existingActive)
        {
            activeVersion.IsActive = false;
        }

        var artifactRecord = new ScenarioArtifactVersionRecord
        {
            Id = Guid.NewGuid(),
            ScenarioKey = normalizedScenarioKey,
            ArtifactType = artifactType,
            ArtifactName = normalizedArtifactName,
            ArtifactVersion = normalizedArtifactVersion,
            RelativePath = normalizedRelativePath,
            SourceFlowVersionId = sourceFlowVersionId,
            ChecksumSha256 = string.IsNullOrWhiteSpace(checksumSha256) ? null : checksumSha256.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsActive = true,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        data.ScenarioArtifactVersions.Add(artifactRecord);
        SaveData(data);

        return Task.FromResult(artifactRecord);
    }

    public Task<List<ScenarioArtifactVersionRecord>> GetScenarioArtifactHistoryAsync(string scenarioKey, ScenarioArtifactType? artifactType = null)
    {
        if (string.IsNullOrWhiteSpace(scenarioKey))
            return Task.FromResult(new List<ScenarioArtifactVersionRecord>());

        var normalizedScenarioKey = scenarioKey.Trim();
        var data = LoadData();
        var query = data.ScenarioArtifactVersions
            .Where(item => item.ScenarioKey.Equals(normalizedScenarioKey, StringComparison.OrdinalIgnoreCase));

        if (artifactType.HasValue)
        {
            query = query.Where(item => item.ArtifactType == artifactType.Value);
        }

        var result = query
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        return Task.FromResult(result);
    }

    public Task MarkScenarioArtifactActiveAsync(Guid artifactVersionId)
    {
        var data = LoadData();
        var target = data.ScenarioArtifactVersions.FirstOrDefault(item => item.Id == artifactVersionId);
        if (target == null)
            return Task.CompletedTask;

        foreach (var candidate in data.ScenarioArtifactVersions.Where(item =>
                     item.ScenarioKey.Equals(target.ScenarioKey, StringComparison.OrdinalIgnoreCase) &&
                     item.ArtifactType == target.ArtifactType &&
                     item.ArtifactName.Equals(target.ArtifactName, StringComparison.OrdinalIgnoreCase)))
        {
            candidate.IsActive = candidate.Id == target.Id;
        }

        SaveData(data);
        return Task.CompletedTask;
    }

    public Task<ScenarioPackageManifest?> BuildScenarioManifestAsync(
        string scenarioKey,
        string scenarioName,
        string description,
        string packageVersion,
        string createdBy = "System")
    {
        if (string.IsNullOrWhiteSpace(scenarioKey))
            return Task.FromResult<ScenarioPackageManifest?>(null);

        var normalizedScenarioKey = scenarioKey.Trim();
        var data = LoadData();
        var activeRecords = data.ScenarioArtifactVersions
            .Where(item => item.IsActive && item.ScenarioKey.Equals(normalizedScenarioKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        if (activeRecords.Count == 0)
            return Task.FromResult<ScenarioPackageManifest?>(null);

        var assets = activeRecords
            .GroupBy(item => (item.ArtifactType, ArtifactName: item.ArtifactName.ToLowerInvariant()))
            .Select(group => group.OrderByDescending(item => item.CreatedAtUtc).First())
            .Select(item => new ScenarioPackageAsset
            {
                ArtifactType = item.ArtifactType,
                ArtifactName = item.ArtifactName,
                ArtifactVersion = item.ArtifactVersion,
                RelativePath = item.RelativePath,
                Required = !TryReadBoolMetadata(item.Metadata, "optional"),
                ChecksumSha256 = item.ChecksumSha256,
                Metadata = new Dictionary<string, string>(item.Metadata)
            })
            .OrderBy(item => item.ArtifactType)
            .ThenBy(item => item.ArtifactName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var constraints = BuildConstraints(activeRecords);
        var manifest = new ScenarioPackageManifest
        {
            SchemaVersion = "1.0",
            ScenarioKey = normalizedScenarioKey,
            ScenarioName = scenarioName,
            Version = packageVersion,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            Assets = assets,
            Constraints = constraints
        };

        return Task.FromResult<ScenarioPackageManifest?>(manifest);
    }

    private static ScenarioPackageConstraints BuildConstraints(List<ScenarioArtifactVersionRecord> artifactRecords)
    {
        var constraints = new ScenarioPackageConstraints();
        foreach (var artifact in artifactRecords)
        {
            if (artifact.Metadata.TryGetValue("requiredLabels", out var requiredLabelsValue))
            {
                constraints.RequiredLabels = SplitCsv(requiredLabelsValue);
            }

            if (artifact.Metadata.TryGetValue("expectedSequence", out var expectedSequenceValue))
            {
                constraints.ExpectedSequence = SplitCsv(expectedSequenceValue);
            }

            if (artifact.Metadata.TryGetValue("expectedDetectionCount", out var expectedDetectionCountValue) &&
                int.TryParse(expectedDetectionCountValue, out var expectedCount))
            {
                constraints.ExpectedDetectionCount = expectedCount;
            }

            if (artifact.Metadata.TryGetValue("judgeOperatorType", out var judgeOperatorTypeValue))
            {
                constraints.JudgeOperatorType = judgeOperatorTypeValue;
            }
        }

        return constraints;
    }

    private static bool TryReadBoolMetadata(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
    }

    private static List<string> SplitCsv(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Trim().Replace('\\', '/');
    }
}
