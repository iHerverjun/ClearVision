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
