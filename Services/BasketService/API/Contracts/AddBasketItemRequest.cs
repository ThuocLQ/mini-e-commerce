namespace BasketService.API.Contracts;

public sealed record AddBasketItemRequest(
    string ProductId,
    int Quantity);
