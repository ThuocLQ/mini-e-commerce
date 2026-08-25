using CatalogService.Application.Abstractions;
using CatalogService.Domain.Products;
using CatalogService.Domain.Outbox;
using BuildingBlocks.Contracts.Events.Inventory;
using System.Data;
using System.Text.Json;
using MediatR;

namespace CatalogService.Application.Products.CreateProduct;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ICatalogUnitOfWork _unitOfWork;
    private readonly ICatalogOutboxRepository _outboxRepository;

    public CreateProductHandler(
        IProductRepository productRepository,
        ICatalogUnitOfWork unitOfWork,
        ICatalogOutboxRepository outboxRepository)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _outboxRepository = outboxRepository;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(
            Guid.NewGuid().ToString(),
            request.Name,
            request.Description ?? string.Empty,
            request.Price,
            request.StockQuantity);

        var created = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var persisted = await _productRepository.CreateAsync(product, transaction, cancellationToken);
            var integrationEvent = new InventoryItemProvisionRequestedIntegrationEvent
            {
                ProductId = persisted.Id,
                InitialStockQuantity = persisted.StockQuantity
            };
            await _outboxRepository.AddAsync(new CatalogOutboxMessage
            {
                Id = integrationEvent.EventId,
                OccurredAtUtc = integrationEvent.OccurredAtUtc,
                Type = integrationEvent.GetType().FullName!,
                Content = JsonSerializer.Serialize(integrationEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                NextAttemptAtUtc = integrationEvent.OccurredAtUtc
            }, transaction, cancellationToken);
            return persisted;
        }, cancellationToken);

        return ProductMapper.ToDto(created);
    }
}
