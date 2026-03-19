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
}
