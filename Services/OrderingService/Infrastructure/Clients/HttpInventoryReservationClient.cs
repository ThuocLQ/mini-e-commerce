using System.Net;
using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Inventory;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpInventoryReservationClient : IInventoryReservationClient
{
    private readonly HttpClient _httpClient;
    public HttpInventoryReservationClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<InventoryReservationResponse> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/_internal/inventory/reservations", new { orderId, items, expiresAtUtc }, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var rejected = await response.Content.ReadFromJsonAsync<InventoryReservationResponse>(cancellationToken: cancellationToken);
                return rejected ?? new InventoryReservationResponse(false, "Inventory reservation was rejected.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryReservationResponse>(cancellationToken: cancellationToken)
                   ?? throw new HttpRequestException("InventoryService returned an invalid inventory reservation response.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            throw new InventoryUnavailableException(ex);
        }
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default) => PostAsync(orderId, "release", cancellationToken);
    public Task CommitAsync(Guid orderId, CancellationToken cancellationToken = default) => PostAsync(orderId, "commit", cancellationToken);

    private async Task PostAsync(Guid orderId, string operation, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"/_internal/inventory/reservations/{orderId:D}/{operation}", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            throw new InventoryUnavailableException(ex);
        }
    }
}
