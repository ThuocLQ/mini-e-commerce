using MediatR;

namespace InventoryService.Application.Inventory.GetInventoryItems;

public sealed record GetInventoryItemsQuery : IRequest<IReadOnlyList<InventoryItemDto>>;

public sealed record InventoryItemDto(
    string ProductId,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTime UpdatedAtUtc);
