using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace PaymentService.Infrastructure.Providers;

public sealed class PayPalApiClient
{
    public const string HttpClientName = "PayPal";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PayPalOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc = DateTimeOffset.MinValue;

    public PayPalApiClient(IHttpClientFactory httpClientFactory, IOptions<PayPalOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<JsonDocument> SendAuthorizedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("The payment provider could not process the request.");
        }

        return JsonDocument.Parse(content);
    }

    public async Task<bool> VerifyWebhookSignatureAsync(IHeaderDictionary headers, string rawBody, CancellationToken cancellationToken)
    {
        var transmissionId = Header(headers, "PAYPAL-TRANSMISSION-ID");
        var transmissionTime = Header(headers, "PAYPAL-TRANSMISSION-TIME");
        var certificateUrl = Header(headers, "PAYPAL-CERT-URL");
        var authAlgorithm = Header(headers, "PAYPAL-AUTH-ALGO");
        var transmissionSignature = Header(headers, "PAYPAL-TRANSMISSION-SIG");
        return await VerifyWebhookSignatureCoreAsync(
            transmissionId,
            transmissionTime,
            certificateUrl,
            authAlgorithm,
            transmissionSignature,
            rawBody,
            cancellationToken);
    }

    private async Task<bool> VerifyWebhookSignatureCoreAsync(
        string? transmissionId,
        string? transmissionTime,
        string? certificateUrl,
        string? authAlgorithm,
        string? transmissionSignature,
        string rawBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transmissionId) ||
            string.IsNullOrWhiteSpace(transmissionTime) ||
            string.IsNullOrWhiteSpace(certificateUrl) ||
            string.IsNullOrWhiteSpace(authAlgorithm) ||
            string.IsNullOrWhiteSpace(transmissionSignature) ||
            string.IsNullOrWhiteSpace(_options.WebhookId))
        {
            return false;
        }

        using var webhookEvent = JsonDocument.Parse(rawBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/notifications/verify-webhook-signature")
        {
            Content = JsonContent.Create(new
            {
                auth_algo = authAlgorithm,
                cert_url = certificateUrl,
                transmission_id = transmissionId,
                transmission_sig = transmissionSignature,
                transmission_time = transmissionTime,
                webhook_id = _options.WebhookId,
                webhook_event = webhookEvent.RootElement.Clone()
            })
        };

        using var response = await SendAuthorizedAsync(request, cancellationToken);
        return response.RootElement.TryGetProperty("verification_status", out var status) &&
               string.Equals(status.GetString(), "SUCCESS", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _accessToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")])
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("The payment provider could not authenticate the merchant account.");
            }

            using var document = JsonDocument.Parse(content);
            var accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 300;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("The payment provider did not return an access token.");
            }

            _accessToken = accessToken;
            _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn));
            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string? Header(IHeaderDictionary headers, string name) =>
        headers.TryGetValue(name, out var value) ? value.ToString() : null;
}