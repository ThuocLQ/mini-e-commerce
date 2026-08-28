using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Inventory;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpInventoryAvailabilityClient(HttpClient httpClient) : IInventoryAvailabilityClient
{
    public async Task<IReadOnlyList<InventoryAvailabilityItem>> GetAvailabilityAsync(
        IReadOnlyList<InventoryAvailabilityRequestItem> items,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/_internal/inventory/availability",
                new InventoryAvailabilityRequest(items.Select(item => new InventoryAvailabilityRequestItemBody(item.ProductId, item.Quantity)).ToList()),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<InventoryAvailabilityResponse>(cancellationToken: cancellationToken)
                       ?? throw new HttpRequestException("InventoryService returned an empty availability response.");
            var expectedProductIds = items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
            var returnedProductIds = body.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
            if (body.Items.Count != items.Count || !expectedProductIds.SequenceEqual(returnedProductIds))
            {
                throw new HttpRequestException("InventoryService returned an invalid availability response.");
            }

            return body.Items
                .Select(item => new InventoryAvailabilityItem(item.ProductId, item.Available))
                .ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            throw new InventoryUnavailableException(ex);
        }
    }

    private sealed record InventoryAvailabilityRequest(IReadOnlyList<InventoryAvailabilityRequestItemBody> Items);

    private sealed record InventoryAvailabilityRequestItemBody(Guid ProductId, int Quantity);

    private sealed record InventoryAvailabilityResponse(IReadOnlyList<InventoryAvailabilityResponseItem> Items);

    private sealed record InventoryAvailabilityResponseItem(Guid ProductId, bool Available);
}
