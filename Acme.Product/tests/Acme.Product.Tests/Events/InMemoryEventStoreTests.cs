using Acme.Product.Core.Events;
using Acme.Product.Infrastructure.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Events;

public class InMemoryEventStoreTests
{
    [Fact]
    public void Append_SameEventInstance_ReusesSequenceId()
    {
        var store = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var projectId = Guid.NewGuid();
        var evt = new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            ProcessedCount = 1
        };

        var first = store.Append(projectId, evt);
        var second = store.Append(projectId, evt);

        first.Should().Be(second);
        store.GetEventsAfter(projectId, 0).Should().ContainSingle();
    }

    [Fact]
    public void GetEventsAfter_ReturnsStoredEventsInSequenceOrder()
    {
        var store = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var projectId = Guid.NewGuid();

        var first = new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            ProcessedCount = 1
        };
        var second = new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            ProcessedCount = 2
        };

        var firstSequence = store.Append(projectId, first);
        var secondSequence = store.Append(projectId, second);

        var replay = store.GetEventsAfter(projectId, firstSequence);

        replay.Select(e => e.SequenceId).Should().Equal(secondSequence);
        replay.Select(e => ((InspectionProgressEvent)e.Event).ProcessedCount).Should().Equal(2);
    }
}
