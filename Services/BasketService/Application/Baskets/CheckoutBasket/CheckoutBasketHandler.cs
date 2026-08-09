using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.CheckoutBasket;

public sealed class CheckoutBasketHandler : IRequestHandler<CheckoutBasketCommand, bool>
{
    private readonly IBasketRepository _repository;

    public CheckoutBasketHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(CheckoutBasketCommand request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException("Expected basket version cannot be negative.");
        }

        return _repository.TryDeleteBasketAsync(request.UserId, request.ExpectedVersion, cancellationToken);
    }
}
