using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Services;

public class InspectionRuntimeCoordinatorTests
{
    [Fact]
    public async Task TryStartAsync_ForSameProjectTwice_ReturnsAlreadyRunningOnSecondCall()
    {
        var coordinator = new InspectionRuntimeCoordinator(NullLogger<InspectionRuntimeCoordinator>.Instance);
        var projectId = Guid.NewGuid();

        var first = await coordinator.TryStartAsync(projectId, Guid.NewGuid(), CancellationToken.None);
        var second = await coordinator.TryStartAsync(projectId, Guid.NewGuid(), CancellationToken.None);

        first.Should().Be(StartResult.Success);
        second.Should().Be(StartResult.AlreadyRunning);
        coordinator.GetState(projectId)!.Status.Should().Be(RuntimeStatus.Starting);
    }

    [Fact]
    public async Task MarkAsStopped_RemovesStateAfterScheduledCleanup()
    {
        var coordinator = new InspectionRuntimeCoordinator(NullLogger<InspectionRuntimeCoordinator>.Instance);
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var start = await coordinator.TryStartAsync(projectId, sessionId, CancellationToken.None);
        start.Should().Be(StartResult.Success);

        coordinator.MarkAsStopped(projectId, sessionId);

        var started = DateTime.UtcNow;
        while (coordinator.GetState(projectId) != null)
        {
            if (DateTime.UtcNow - started > TimeSpan.FromSeconds(2))
            {
                throw new TimeoutException("Coordinator cleanup did not remove the session in time.");
            }

            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task StaleSessionCleanup_DoesNotRemoveReplacementSession()
    {
        var coordinator = new InspectionRuntimeCoordinator(NullLogger<InspectionRuntimeCoordinator>.Instance);
        var projectId = Guid.NewGuid();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();

        (await coordinator.TryStartAsync(projectId, firstSessionId, CancellationToken.None))
            .Should().Be(StartResult.Success);

        coordinator.MarkAsStopped(projectId, firstSessionId);

        (await coordinator.TryStartAsync(projectId, secondSessionId, CancellationToken.None))
            .Should().Be(StartResult.Success);

        await Task.Delay(200);

        var replacementState = coordinator.GetState(projectId);
        replacementState.Should().NotBeNull();
        replacementState!.SessionId.Should().Be(secondSessionId);
        replacementState.Status.Should().Be(RuntimeStatus.Starting);

        coordinator.MarkAsStopped(projectId, firstSessionId);

        coordinator.GetState(projectId)!.SessionId.Should().Be(secondSessionId);
    }
}
