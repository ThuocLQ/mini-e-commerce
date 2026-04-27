namespace BasketService.DTOs;

public record AddBasketItemRequest(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal Price);