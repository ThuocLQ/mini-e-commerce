using System.Net.Http.Json;
using CatalogService.Application.Abstractions;

namespace CatalogService.Infrastructure.Clients;

public sealed class HttpInventoryStockClient(HttpClient httpClient) : IInventoryStockClient
{
    public async Task SetStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync(
                $"/_internal/inventory/items/{Uri.EscapeDataString(productId)}/stock",
                new { stockQuantity },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is HttpRequestException or TaskCanceledException)
        {
            throw new InventoryUnavailableException(exception);
        }
    }
}
