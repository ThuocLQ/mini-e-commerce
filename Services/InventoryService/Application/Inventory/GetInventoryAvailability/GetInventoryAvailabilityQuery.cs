using InventoryService.Application.Abstractions;
using MediatR;

namespace InventoryService.Application.Inventory.GetInventoryAvailability;

public sealed record GetInventoryAvailabilityQuery(
    IReadOnlyList<InventoryAvailabilityRequestItem> Items) : IRequest<IReadOnlyList<InventoryAvailabilityDto>>;

public sealed record InventoryAvailabilityRequestItem(Guid ProductId, int Quantity);

public sealed record InventoryAvailabilityDto(Guid ProductId, bool Available);

public sealed class GetInventoryAvailabilityHandler(IInventoryItemRepository repository)
    : IRequestHandler<GetInventoryAvailabilityQuery, IReadOnlyList<InventoryAvailabilityDto>>
{
    private const int MaximumItemsPerRequest = 100;

    public Task<IReadOnlyList<InventoryAvailabilityDto>> Handle(
        GetInventoryAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Items is not { Count: > 0 and <= MaximumItemsPerRequest })
        {
            throw new ArgumentException($"Between 1 and {MaximumItemsPerRequest} inventory items are required.");
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            throw new ArgumentException("Each inventory item requires a product id and a positive quantity.");
        }

        if (request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Count)
        {
            throw new ArgumentException("Inventory availability items must not contain duplicate product ids.");
        }

        return repository.GetAvailabilityAsync(request.Items, cancellationToken);
    }
}