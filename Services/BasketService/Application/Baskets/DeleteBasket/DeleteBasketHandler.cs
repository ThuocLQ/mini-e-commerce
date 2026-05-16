using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.DeleteBasket;

public sealed class DeleteBasketHandler : IRequestHandler<DeleteBasketCommand, bool>
{
    private readonly IBasketRepository _repository;

    public DeleteBasketHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
    {
        return _repository.DeleteBasketAsync(request.UserId, cancellationToken);
    }
}
