using System.Collections.Concurrent;
using Acme.Product.Core.Events;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Events;

public interface IEventStore
{
    long Append(Guid projectId, IInspectionEvent evt);
    IReadOnlyList<StoredInspectionEvent> GetEventsAfter(Guid projectId, long sequenceId);
    void Cleanup(Guid projectId);
}

public sealed record StoredInspectionEvent(long SequenceId, IInspectionEvent Event, DateTime StoredAt);

public sealed class InMemoryEventStore : IEventStore, IDisposable
{
    private const int MaxEventsPerProject = 100;
    private const int MaxProjects = 50;

    private readonly ILogger<InMemoryEventStore> _logger;
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<StoredInspectionEvent>> _store = new();
    private readonly ConcurrentDictionary<IInspectionEvent, StoredInspectionEvent> _eventIndex =
        new(ReferenceEqualityComparer.Instance);
    private long _globalSequenceId;

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger;
    }

    public long Append(Guid projectId, IInspectionEvent evt)
    {
        if (_eventIndex.TryGetValue(evt, out var existing))
        {
            return existing.SequenceId;
        }

        var queue = _store.GetOrAdd(projectId, _ => new ConcurrentQueue<StoredInspectionEvent>());
        var stored = new StoredInspectionEvent(
            Interlocked.Increment(ref _globalSequenceId),
            evt,
            DateTime.UtcNow);

        if (!_eventIndex.TryAdd(evt, stored))
        {
            return _eventIndex[evt].SequenceId;
        }

        queue.Enqueue(stored);

        while (queue.Count > MaxEventsPerProject && queue.TryDequeue(out var removed))
        {
            _eventIndex.TryRemove(removed.Event, out _);
        }

        if (_store.Count > MaxProjects)
        {
            _ = Task.Run(CleanupOldProjects);
        }

        _logger.LogDebug(
            "[EventStore] Stored event {EventType} seq={SequenceId} project={ProjectId}",
            evt.GetType().Name,
            stored.SequenceId,
            projectId);

        return stored.SequenceId;
    }

    public IReadOnlyList<StoredInspectionEvent> GetEventsAfter(Guid projectId, long sequenceId)
    {
        if (!_store.TryGetValue(projectId, out var queue))
        {
            return Array.Empty<StoredInspectionEvent>();
        }

        return queue
            .Where(e => e.SequenceId > sequenceId)
            .OrderBy(e => e.SequenceId)
            .ToList();
    }

    public void Cleanup(Guid projectId)
    {
        if (!_store.TryRemove(projectId, out var queue))
        {
            return;
        }

        while (queue.TryDequeue(out var stored))
        {
            _eventIndex.TryRemove(stored.Event, out _);
        }

        _logger.LogDebug("[EventStore] Cleaned project history {ProjectId}", projectId);
    }

    public void Dispose()
    {
        foreach (var projectId in _store.Keys.ToArray())
        {
            Cleanup(projectId);
        }
    }

    private void CleanupOldProjects()
    {
        try
        {
            var toRemove = _store
                .OrderBy(kvp => kvp.Value.LastOrDefault()?.StoredAt ?? DateTime.MinValue)
                .Take(Math.Max(0, _store.Count - MaxProjects / 2))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var projectId in toRemove)
            {
                Cleanup(projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventStore] Cleanup failed");
        }
    }
}
