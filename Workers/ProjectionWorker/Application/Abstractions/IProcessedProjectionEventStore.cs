namespace ProjectionWorker.Application.Abstractions;

public interface IProcessedProjectionEventStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        ProcessedProjectionEvent processedEvent,
        CancellationToken cancellationToken = default);
}
