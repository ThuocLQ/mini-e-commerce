using BasketService.Application.Abstractions;
using BasketService.Application.Catalog;
using BasketService.Domain.Baskets;
using MediatR;

namespace BasketService.Application.Baskets.AddBasketItem;

public sealed class AddBasketItemHandler : IRequestHandler<AddBasketItemCommand, BasketDto>
{
    private readonly IBasketRepository _repository;
    private readonly ICatalogProductClient _catalogProductClient;

    public AddBasketItemHandler(
        IBasketRepository repository,
        ICatalogProductClient catalogProductClient)
    {
        _repository = repository;
        _catalogProductClient = catalogProductClient;
    }

    public async Task<BasketDto> Handle(AddBasketItemCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            throw new ArgumentException("ProductId is required.");
        }

        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.");
        }

        var product = await _catalogProductClient.GetProductByIdAsync(
            request.ProductId,
            request.Mode,
            cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException();
        }

        if (product.Price < 0)
        {
            throw new ArgumentException("Price must be greater than or equal to 0.");
        }

        var basket = await _repository.GetBasketAsync(request.UserId, cancellationToken);
        basket.AddItem(new BasketItem
        {
            ProductId = request.ProductId,
            ProductName = product.Name,
            Quantity = request.Quantity,
            Price = product.Price
        });

        var updatedBasket = await _repository.UpdateBasketAsync(basket, cancellationToken);

        return BasketDto.FromDomain(updatedBasket);
    }
}
