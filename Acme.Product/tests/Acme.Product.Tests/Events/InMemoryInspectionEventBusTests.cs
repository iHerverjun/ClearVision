using Acme.Product.Core.Events;
using Acme.Product.Infrastructure.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Events;

public class InMemoryInspectionEventBusTests
{
    [Fact]
    public async Task PublishAsync_InterfaceSubscription_InvokesHandlerAndStoresEvent()
    {
        var store = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var bus = new InMemoryInspectionEventBus(
            NullLogger<InMemoryInspectionEventBus>.Instance,
            store);
        var projectId = Guid.NewGuid();
        IInspectionEvent? received = null;

        using var subscription = bus.SubscribeInterface<IInspectionEvent>((evt, _) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var evt = new InspectionStateChangedEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            OldState = "Starting",
            NewState = "Running"
        };

        await bus.PublishAsync(evt);

        received.Should().BeSameAs(evt);
        store.GetEventsAfter(projectId, 0).Select(e => e.Event).Should().ContainSingle().Which.Should().BeSameAs(evt);
    }

    [Fact]
    public async Task PublishAsync_WhenOneHandlerFails_StillInvokesRemainingHandlers()
    {
        var store = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var bus = new InMemoryInspectionEventBus(
            NullLogger<InMemoryInspectionEventBus>.Instance,
            store);
        var invoked = false;

        using var _ = bus.Subscribe<InspectionProgressEvent>((_, _) => throw new InvalidOperationException("boom"));
        using var __ = bus.Subscribe<InspectionProgressEvent>((_, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        var act = () => bus.PublishAsync(new InspectionProgressEvent
        {
            ProjectId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProcessedCount = 1
        });

        await act.Should().ThrowAsync<AggregateException>();
        invoked.Should().BeTrue();
    }
}
