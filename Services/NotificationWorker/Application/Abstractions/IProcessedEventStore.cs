namespace NotificationWorker.Application.Abstractions;

public interface IProcessedEventStore
{
    Task<ProcessedEventStartResult> TryStartProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}

public enum ProcessedEventStartResult
{
    Started = 1,
    AlreadyProcessing = 2,
    AlreadyProcessed = 3
}
