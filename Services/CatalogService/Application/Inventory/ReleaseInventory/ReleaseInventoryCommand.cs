using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Inventory.ReleaseInventory;

public sealed record ReleaseInventoryCommand(Guid OrderId) : IRequest;

public sealed class ReleaseInventoryHandler : IRequestHandler<ReleaseInventoryCommand>
{
    private readonly IInventoryReservationRepository _repository;
    public ReleaseInventoryHandler(IInventoryReservationRepository repository) => _repository = repository;
    public Task Handle(ReleaseInventoryCommand request, CancellationToken cancellationToken) =>
        request.OrderId == Guid.Empty ? throw new ArgumentException("Order id is required.") : _repository.ReleaseAsync(request.OrderId, cancellationToken);
}
