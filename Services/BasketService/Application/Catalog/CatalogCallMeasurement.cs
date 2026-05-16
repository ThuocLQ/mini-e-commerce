namespace BasketService.Application.Catalog;

public sealed record CatalogCallMeasurement(
    bool Success,
    long ElapsedMs);
