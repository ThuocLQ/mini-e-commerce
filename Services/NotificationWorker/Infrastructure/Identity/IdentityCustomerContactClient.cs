using System.Net;
using System.Net.Http.Json;

namespace NotificationWorker.Infrastructure.Identity;

public interface ICustomerContactClient
{
    Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed record CustomerContact(Guid CustomerId, string Email, bool IsEmailVerified);

public sealed class IdentityCustomerContactClient : ICustomerContactClient
{
    private const string InternalApiKeyHeader = "X-MicroShop-Internal-Key";

    private readonly HttpClient _httpClient;
    private readonly string _internalApiKey;

    public IdentityCustomerContactClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _internalApiKey = configuration["InternalApi:Key"]
            ?? throw new InvalidOperationException("InternalApi:Key is missing.");
    }

    public async Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/customers/{customerId:D}/contact");
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _internalApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var contact = await response.Content.ReadFromJsonAsync<CustomerContact>(cancellationToken: cancellationToken);
        if (contact is null || contact.CustomerId != customerId || string.IsNullOrWhiteSpace(contact.Email))
        {
            throw new InvalidOperationException($"Identity contact response was invalid for customer {customerId:D}.");
        }

        return contact;
    }
}