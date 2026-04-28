namespace BasketService.DTOs;

public record AddBasketItemRequest(
    string ProductId,
    int Quantity);