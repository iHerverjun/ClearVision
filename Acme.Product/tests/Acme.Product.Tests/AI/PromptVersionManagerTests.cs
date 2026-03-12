using Acme.Product.Infrastructure.AI;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class PromptVersionManagerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public async Task CreateActivateAndRecordMetrics_ShouldPersistAcrossReload()
    {
        var tempDir = CreateTempDir();
        var sut = new PromptVersionManager(tempDir);

        var version1 = await sut.CreateVersionAsync("V1", "prompt-1", "baseline", "tester");
        var version2 = await sut.CreateVersionAsync("V2", "prompt-2", "optimized", "tester");

        await sut.ActivateVersionAsync(version2.Id);
        await sut.RecordMetricsAsync(version2.Id, success: true, tokenUsage: 120, latencyMs: 40);
        await sut.RecordMetricsAsync(version2.Id, success: false, tokenUsage: 30, latencyMs: 10);

        var reloaded = new PromptVersionManager(tempDir);
        var active = await reloaded.GetActiveVersionAsync();

        active.Id.Should().Be(version2.Id);
        active.Metrics.TotalCalls.Should().Be(2);
        active.Metrics.SuccessCalls.Should().Be(1);
        active.Metrics.TotalTokenUsage.Should().Be(150);
        active.Metrics.TotalLatencyMs.Should().Be(50);
    }

    [Fact]
    public async Task DeleteVersionAsync_WhenDeletingActive_ShouldPromoteNewestRemaining()
    {
        var tempDir = CreateTempDir();
        var sut = new PromptVersionManager(tempDir);

        var version1 = await sut.CreateVersionAsync("V1", "prompt-1", "baseline", "tester");
        await Task.Delay(20);
        var version2 = await sut.CreateVersionAsync("V2", "prompt-2", "optimized", "tester");

        await sut.ActivateVersionAsync(version1.Id);
        await sut.DeleteVersionAsync(version1.Id);

        var active = await sut.GetActiveVersionAsync();
        active.Id.Should().Be(version2.Id);

        var versions = await sut.ListVersionsAsync();
        versions.Should().ContainSingle(v => v.Id == version2.Id);
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
        var dir = Path.Combine(Path.GetTempPath(), $"cv-prompt-versions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }
}
