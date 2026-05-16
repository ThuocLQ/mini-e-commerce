namespace BasketService.Application.Baskets;

public sealed record CatalogProductValidateResult(
    bool Valid,
    string? Message,
    string? ProductId,
    string? ProductName,
    decimal Price,
    string? Description);
