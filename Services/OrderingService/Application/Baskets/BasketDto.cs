namespace OrderingService.Application.Baskets;

public sealed record BasketDto(string UserId, IReadOnlyList<BasketItemDto> Items);

public sealed record BasketItemDto(string ProductId, string? ProductName, decimal Price, int Quantity);
