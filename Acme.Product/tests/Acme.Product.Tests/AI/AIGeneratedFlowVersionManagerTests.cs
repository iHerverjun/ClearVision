using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class AIGeneratedFlowVersionManagerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public async Task SaveVersionAsync_ShouldIncrementVersionAndReturnDescendingHistory()
    {
        var tempDir = CreateTempDir();
        var sut = new AIGeneratedFlowVersionManager(tempDir);
        var flow = new OperatorFlow("AI Flow");
        var prompt = new PromptVersionInfo { VersionId = Guid.NewGuid(), Name = "Prompt V1" };
        var telemetry = new WorkflowTelemetry { TotalTimeMs = 120, LLMTokenUsage = 32 };

        var version1 = await sut.SaveVersionAsync(flow, "req-1", prompt, "OpenAI", telemetry, "tester");
        var version2 = await sut.SaveVersionAsync(flow, "req-2", prompt, "OpenAI", telemetry, "tester");

        version1.VersionNumber.Should().Be(1);
        version2.VersionNumber.Should().Be(2);

        var history = await sut.GetFlowHistoryAsync(flow.Id);
        history.Select(v => v.VersionNumber).Should().Equal(2, 1);
    }

    [Fact]
    public async Task MarkAsDeployedAsync_ShouldKeepOnlyLatestDeployedVersion()
    {
        var tempDir = CreateTempDir();
        var sut = new AIGeneratedFlowVersionManager(tempDir);
        var flow = new OperatorFlow("AI Flow");
        var prompt = new PromptVersionInfo { VersionId = Guid.NewGuid(), Name = "Prompt V1" };
        var telemetry = new WorkflowTelemetry { TotalTimeMs = 80, LLMTokenUsage = 16 };

        var version1 = await sut.SaveVersionAsync(flow, "req-1", prompt, "OpenAI", telemetry, "tester");
        var version2 = await sut.SaveVersionAsync(flow, "req-2", prompt, "OpenAI", telemetry, "tester");

        await sut.MarkAsDeployedAsync(version1.Id);
        await sut.MarkAsDeployedAsync(version2.Id);

        var history = await sut.GetFlowHistoryAsync(flow.Id);
        history.Should().ContainSingle(v => v.Id == version2.Id && v.IsDeployed);
        history.Should().ContainSingle(v => v.Id == version1.Id && !v.IsDeployed);
    }

    [Fact]
    public async Task SaveScenarioArtifactVersionAsync_ShouldKeepOnlyLatestActivePerArtifact()
    {
        var tempDir = CreateTempDir();
        var sut = new AIGeneratedFlowVersionManager(tempDir);
        const string scenarioKey = "wire-sequence-terminal";

        var modelV1 = await sut.SaveScenarioArtifactVersionAsync(
            scenarioKey,
            ScenarioArtifactType.Model,
            "wire-seq-yolo",
            "1.0.0",
            "models/wire-seq-yolo-v1.onnx");

        var modelV2 = await sut.SaveScenarioArtifactVersionAsync(
            scenarioKey,
            ScenarioArtifactType.Model,
            "wire-seq-yolo",
            "1.1.0",
            "models/wire-seq-yolo-v1.1.onnx");

        var history = await sut.GetScenarioArtifactHistoryAsync(scenarioKey, ScenarioArtifactType.Model);

        history.Should().HaveCount(2);
        history.Should().ContainSingle(item => item.Id == modelV2.Id && item.IsActive);
        history.Should().ContainSingle(item => item.Id == modelV1.Id && !item.IsActive);
    }

    [Fact]
    public async Task BuildScenarioManifestAsync_ShouldUseCurrentActiveArtifactsAndConstraints()
    {
        var tempDir = CreateTempDir();
        var sut = new AIGeneratedFlowVersionManager(tempDir);
        const string scenarioKey = "wire-sequence-terminal";

        await sut.SaveScenarioArtifactVersionAsync(
            scenarioKey,
            ScenarioArtifactType.Template,
            "terminal-wire-sequence-template",
            "1.0.0",
            "template/terminal-wire-sequence.flow.template.json",
            metadata: new Dictionary<string, string>
            {
                ["requiredResources"] = "DeepLearning.ModelPath,DeepLearning.LabelsPath"
            });

        var modelV1 = await sut.SaveScenarioArtifactVersionAsync(
            scenarioKey,
            ScenarioArtifactType.Model,
            "wire-seq-yolo",
            "1.0.0",
            "models/wire-seq-yolo-v1.onnx");

        var modelV2 = await sut.SaveScenarioArtifactVersionAsync(
            scenarioKey,
            ScenarioArtifactType.Model,
            "wire-seq-yolo",
            "1.1.0",
            "models/wire-seq-yolo-v1.1.onnx",
            metadata: new Dictionary<string, string>
            {
                ["requiredLabels"] = "Wire_Brown,Wire_Black,Wire_Blue",
                ["expectedSequence"] = "Wire_Brown,Wire_Black,Wire_Blue",
                ["expectedDetectionCount"] = "3",
                ["judgeOperatorType"] = "DetectionSequenceJudge"
            });

        await sut.MarkScenarioArtifactActiveAsync(modelV2.Id);
        await sut.MarkScenarioArtifactActiveAsync(modelV1.Id);
        await sut.MarkScenarioArtifactActiveAsync(modelV2.Id);

        var manifest = await sut.BuildScenarioManifestAsync(
            scenarioKey,
            "Terminal Wire Sequence",
            "Wire sequence package",
            "1.0.0",
            createdBy: "tester");

        manifest.Should().NotBeNull();
        manifest!.ScenarioKey.Should().Be(scenarioKey);
        manifest.Assets.Should().ContainSingle(item =>
            item.ArtifactType == ScenarioArtifactType.Model &&
            item.ArtifactVersion == "1.1.0" &&
            item.RelativePath == "models/wire-seq-yolo-v1.1.onnx");

        manifest.Constraints.RequiredLabels.Should().Equal("Wire_Brown", "Wire_Black", "Wire_Blue");
        manifest.Constraints.ExpectedSequence.Should().Equal("Wire_Brown", "Wire_Black", "Wire_Blue");
        manifest.Constraints.ExpectedDetectionCount.Should().Be(3);
        manifest.Constraints.JudgeOperatorType.Should().Be("DetectionSequenceJudge");
        manifest.Constraints.RequiredResources.Should().Equal("DeepLearning.ModelPath", "DeepLearning.LabelsPath");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
            }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cv-flow-versions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }
}
