namespace OrderingService.Application.Baskets;

public sealed record BasketDto(string UserId, Guid BasketId, IReadOnlyList<BasketItemDto> Items, long Version);

public sealed record BasketItemDto(string ProductId, string? ProductName, decimal Price, int Quantity);
