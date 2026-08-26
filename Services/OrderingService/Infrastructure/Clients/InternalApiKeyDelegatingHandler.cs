namespace OrderingService.Infrastructure.Clients;

public sealed class InternalApiKeyDelegatingHandler : DelegatingHandler
{
    private const string HeaderName = "X-MicroShop-Internal-Key";
    private readonly string _key;

    public InternalApiKeyDelegatingHandler(string key)
    {
        _key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Internal API key is required.", nameof(key))
            : key;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, _key);

        return base.SendAsync(request, cancellationToken);
    }
}