using System.Net;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpBasketClient : IBasketClient
{
    private readonly HttpClient _httpClient;

    public HttpBasketClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/baskets/{customerId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BasketDto>(cancellationToken);
    }

    public async Task ClearBasketAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/baskets/{customerId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
    } 
}