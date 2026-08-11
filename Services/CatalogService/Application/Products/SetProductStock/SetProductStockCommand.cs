using MediatR;

namespace CatalogService.Application.Products.SetProductStock;

public sealed record SetProductStockCommand(string Id, int StockQuantity) : IRequest<ProductDto?>;
