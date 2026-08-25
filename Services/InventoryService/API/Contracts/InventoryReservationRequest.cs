namespace InventoryService.API.Contracts;

public sealed record InventoryReservationRequest(
    Guid OrderId,
    IReadOnlyList<InventoryReservationItemRequest> Items,
    DateTime ExpiresAtUtc);

public sealed record InventoryReservationItemRequest(string ProductId, int Quantity);

