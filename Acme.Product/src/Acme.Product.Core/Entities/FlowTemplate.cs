// FlowTemplate.cs
// 流程模板实体
// 定义流程模板的标识、内容与版本信息
// 作者：蘅芜君
namespace Acme.Product.Core.Entities;

/// <summary>
/// 工作流模板定义
/// </summary>
public class FlowTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string FlowJson { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "1.0.0";
    public string? ScenarioKey { get; set; }
    public ScenarioPackageBinding? ScenarioPackage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ScenarioArtifactType
{
    ScenarioPackage = 0,
    Template = 1,
    Model = 2,
    Rule = 3,
    Label = 4,
    Sample = 5,
    Faq = 6
}

public class ScenarioPackageBinding
{
    public string PackageKey { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = "1.0.0";
    public List<string> AssetVersionIds { get; set; } = new();
    public List<string> RequiredResources { get; set; } = new();
}

public class ScenarioPackageManifest
{
    public string SchemaVersion { get; set; } = "1.0";
    public string ScenarioKey { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public List<ScenarioPackageAsset> Assets { get; set; } = new();
    public ScenarioPackageConstraints Constraints { get; set; } = new();
}

public class ScenarioPackageAsset
{
    public ScenarioArtifactType ArtifactType { get; set; }
    public string ArtifactName { get; set; } = string.Empty;
    public string ArtifactVersion { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public string? ChecksumSha256 { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ScenarioPackageConstraints
{
    public List<string> RequiredLabels { get; set; } = new();
    public List<string> ExpectedSequence { get; set; } = new();
    public int? ExpectedDetectionCount { get; set; }
    public string? JudgeOperatorType { get; set; }
}
