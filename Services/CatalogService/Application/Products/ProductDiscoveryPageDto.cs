namespace CatalogService.Application.Products;

public sealed record ProductDiscoveryPageDto(
    IReadOnlyList<ProductDto> Items,
    string? NextCursor,
    int PageSize,
    string Sort);