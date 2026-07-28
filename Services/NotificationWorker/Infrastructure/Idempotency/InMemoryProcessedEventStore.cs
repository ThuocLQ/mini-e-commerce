using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Idempotency;

public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private static readonly TimeSpan ProcessingTtl = TimeSpan.FromMinutes(10);
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ProcessedEventEntry> _events = [];

    public Task<ProcessedEventLeaseAcquisition> TryStartProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_events.TryGetValue(eventId, out var entry)
                || (entry.Status == ProcessedEventStatus.Processing && entry.LeaseExpiresAtUtc <= DateTimeOffset.UtcNow))
            {
                var leaseToken = CreateLeaseToken();
                _events[eventId] = new ProcessedEventEntry(
                    ProcessedEventStatus.Processing,
                    leaseToken,
                    DateTimeOffset.UtcNow.Add(ProcessingTtl));

                return Task.FromResult(new ProcessedEventLeaseAcquisition(
                    ProcessedEventStartResult.Started,
                    leaseToken));
            }

            return Task.FromResult(new ProcessedEventLeaseAcquisition(
                entry.Status == ProcessedEventStatus.Processed
                    ? ProcessedEventStartResult.AlreadyProcessed
                    : ProcessedEventStartResult.AlreadyProcessing));
        }
    }

    public Task<bool> MarkAsProcessedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_events.TryGetValue(eventId, out var entry)
                || entry.Status != ProcessedEventStatus.Processing
                || entry.LeaseToken != leaseToken)
            {
                return Task.FromResult(false);
            }

            _events[eventId] = new ProcessedEventEntry(ProcessedEventStatus.Processed, null, null);
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarkAsFailedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_events.TryGetValue(eventId, out var entry)
                || entry.Status != ProcessedEventStatus.Processing
                || entry.LeaseToken != leaseToken)
            {
                return Task.FromResult(false);
            }

            _events.Remove(eventId);
            return Task.FromResult(true);
        }
    }

    private static string CreateLeaseToken() => $"processing:{Guid.NewGuid():N}";

    private sealed record ProcessedEventEntry(
        ProcessedEventStatus Status,
        string? LeaseToken,
        DateTimeOffset? LeaseExpiresAtUtc);

    private enum ProcessedEventStatus
    {
        Processing = 1,
        Processed = 2
    }
}
