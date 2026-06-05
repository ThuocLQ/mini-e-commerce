using ProjectionWorker.Application.Events;

namespace ProjectionWorker.Application.Abstractions;

public interface IOrderSummaryProjectionRepository
{
    Task ApplyAsync(OrderProjectionEvent orderEvent, CancellationToken cancellationToken = default);
}
