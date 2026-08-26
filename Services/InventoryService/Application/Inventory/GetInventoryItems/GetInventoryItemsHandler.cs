using InventoryService.Application.Abstractions;
using MediatR;

namespace InventoryService.Application.Inventory.GetInventoryItems;

public sealed class GetInventoryItemsHandler(IInventoryItemRepository repository)
    : IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItemDto>>
{
    public Task<IReadOnlyList<InventoryItemDto>> Handle(GetInventoryItemsQuery request, CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);
}
