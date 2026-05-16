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
        try
        {
            var response = await _httpClient.GetAsync($"/basket/{customerId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<BasketDto>(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsDownstreamFailure(ex))
        {
            throw new BasketUnavailableException(ex);
        }
    }

    public async Task ClearBasketAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/basket/{customerId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return;
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsDownstreamFailure(ex))
        {
            throw new BasketUnavailableException(ex);
        }
    }

    private static bool IsDownstreamFailure(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException;
    }
}
