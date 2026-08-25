using MediatR;

namespace InventoryService.Application.Inventory.ReserveInventory;

public sealed record ReserveInventoryCommand(
    Guid OrderId,
    IReadOnlyList<InventoryReservationItemDto> Items,
    DateTime ExpiresAtUtc) : IRequest<InventoryReservationResultDto>;

public sealed record InventoryReservationItemDto(string ProductId, int Quantity);

public sealed record InventoryReservationResultDto(bool Succeeded, string? FailureReason);

