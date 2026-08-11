using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Inventory.ReserveInventory;

public sealed class ReserveInventoryHandler : IRequestHandler<ReserveInventoryCommand, InventoryReservationResultDto>
{
    private readonly IInventoryReservationRepository _repository;

    public ReserveInventoryHandler(IInventoryReservationRepository repository) => _repository = repository;

    public async Task<InventoryReservationResultDto> Handle(ReserveInventoryCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty || request.ExpiresAtUtc <= DateTime.UtcNow || request.Items.Count == 0 ||
            request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0))
        {
            throw new ArgumentException("A future expiry and at least one valid inventory item are required.");
        }

        var items = request.Items
            .GroupBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new InventoryReservationItem(group.Key, group.Sum(item => item.Quantity)))
            .ToList();
        var result = await _repository.ReserveAsync(request.OrderId, items, request.ExpiresAtUtc, cancellationToken);
        return new InventoryReservationResultDto(result.Succeeded, result.FailureReason);
    }
}
