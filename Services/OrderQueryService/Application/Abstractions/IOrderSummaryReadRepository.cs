using OrderQueryService.Application.ReadModels;

namespace OrderQueryService.Application.Abstractions;

public interface IOrderSummaryReadRepository
{
    Task UpsertAsync(
        OrderSummaryReadModel model,
        CancellationToken cancellationToken = default);

    Task<OrderSummaryReadModel?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderSummaryReadModel>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
