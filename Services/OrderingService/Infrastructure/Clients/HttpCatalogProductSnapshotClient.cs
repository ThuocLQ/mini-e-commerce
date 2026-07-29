using System.Net;
using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Catalog;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpCatalogProductSnapshotClient : ICatalogProductSnapshotClient
{
    private readonly HttpClient _httpClient;

    public HttpCatalogProductSnapshotClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogProductSnapshot?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"/products/{productId:D}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<CatalogProductResponse>(
                cancellationToken: cancellationToken);

            if (product is null || !Guid.TryParse(product.Id, out var returnedProductId) || returnedProductId != productId)
            {
                throw new HttpRequestException("CatalogService returned an invalid product response.");
            }

            return new CatalogProductSnapshot(returnedProductId, product.Name, product.Price);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException)
        {
            throw new CatalogUnavailableException(ex);
        }
    }

    private sealed record CatalogProductResponse(
        string Id,
        string Name,
        string Description,
        decimal Price);
}
