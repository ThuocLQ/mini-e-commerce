namespace InventoryService.API.Contracts;

public sealed record InventoryAvailabilityRequest(IReadOnlyList<InventoryAvailabilityItemRequest>? Items);

public sealed record InventoryAvailabilityItemRequest(Guid ProductId, int Quantity);

public sealed record InventoryAvailabilityResponse(IReadOnlyList<InventoryAvailabilityItemResponse> Items);

public sealed record InventoryAvailabilityItemResponse(Guid ProductId, bool Available);