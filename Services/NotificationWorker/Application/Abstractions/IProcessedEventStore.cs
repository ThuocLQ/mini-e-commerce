namespace NotificationWorker.Application.Abstractions;

public interface IProcessedEventStore
{
    Task<ProcessedEventLeaseAcquisition> TryStartProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsProcessedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsFailedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessedEventLeaseAcquisition(
    ProcessedEventStartResult Result,
    string? LeaseToken = null);

public enum ProcessedEventStartResult
{
    Started = 1,
    AlreadyProcessing = 2,
    AlreadyProcessed = 3
}
