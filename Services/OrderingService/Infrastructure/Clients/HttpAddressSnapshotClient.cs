using System.Net;
using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Addresses;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpAddressSnapshotClient(HttpClient httpClient) : IAddressSnapshotClient
{
    public async Task<CustomerAddressSnapshot?> GetAddressAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"/internal/customers/{customerId:D}/addresses/{addressId:D}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var address = await response.Content.ReadFromJsonAsync<AddressResponse>(cancellationToken: cancellationToken);
            if (address is null || address.AddressId != addressId)
            {
                throw new HttpRequestException("IdentityService returned an invalid address response.");
            }

            return new CustomerAddressSnapshot(
                address.AddressId,
                address.Label,
                address.RecipientName,
                address.Line1,
                address.Line2,
                address.City,
                address.CountryCode,
                address.PostalCode);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            throw new AddressUnavailableException(ex);
        }
    }

    private sealed record AddressResponse(
        Guid AddressId,
        string Label,
        string RecipientName,
        string Line1,
        string? Line2,
        string City,
        string CountryCode,
        string? PostalCode);
}
