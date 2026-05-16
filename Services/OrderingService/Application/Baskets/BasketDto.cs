namespace OrderingService.Application.Baskets;

public sealed record BasketDto(Guid CustomerId, IReadOnlyList<BasketItemDto> Items);

public sealed record BasketItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);