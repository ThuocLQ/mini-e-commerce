using System.Collections.Concurrent;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Idempotency;

public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private readonly ConcurrentDictionary<Guid, ProcessedEventStatus> _events = new();

    public Task<ProcessedEventStartResult> TryStartProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var added = _events.TryAdd(eventId, ProcessedEventStatus.Processing);
        if (added)
        {
            return Task.FromResult(ProcessedEventStartResult.Started);
        }

        var status = _events[eventId];
        var result = status == ProcessedEventStatus.Processed
            ? ProcessedEventStartResult.AlreadyProcessed
            : ProcessedEventStartResult.AlreadyProcessing;

        return Task.FromResult(result);
    }

    public Task MarkAsProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        _events.AddOrUpdate(
            eventId,
            ProcessedEventStatus.Processed,
            (_, _) => ProcessedEventStatus.Processed);

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        _events.TryRemove(eventId, out _);
        return Task.CompletedTask;
    }

    private enum ProcessedEventStatus
    {
        Processing = 1,
        Processed = 2
    }
}
