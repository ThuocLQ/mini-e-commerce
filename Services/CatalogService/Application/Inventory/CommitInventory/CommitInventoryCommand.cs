using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Inventory.CommitInventory;

public sealed record CommitInventoryCommand(Guid OrderId, Guid? MessageId = null) : IRequest;

public sealed class CommitInventoryHandler : IRequestHandler<CommitInventoryCommand>
{
    private readonly IInventoryReservationRepository _repository;
    public CommitInventoryHandler(IInventoryReservationRepository repository) => _repository = repository;
    public Task Handle(CommitInventoryCommand request, CancellationToken cancellationToken) =>
        request.OrderId == Guid.Empty
            ? throw new ArgumentException("Order id is required.")
            : _repository.CommitAsync(request.OrderId, request.MessageId, cancellationToken);
}
