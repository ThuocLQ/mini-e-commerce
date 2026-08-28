using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderingService.Application.Orders.CheckoutQuote;

namespace OrderingService.Infrastructure.CheckoutQuote;

public sealed class HmacCheckoutQuoteTokenService : ICheckoutQuoteTokenService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _signingKey;
    private readonly TimeProvider _timeProvider;

    public HmacCheckoutQuoteTokenService(
        IOptions<CheckoutQuoteOptions> options,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _signingKey = ResolveSigningKey(options.Value.SigningKey, configuration["Jwt:SecretKey"]);
        _timeProvider = timeProvider;
    }

    public string Create(CheckoutQuoteTokenPayload payload)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        var signature = Sign(payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public CheckoutQuoteTokenPayload ReadAndValidate(string token, CheckoutQuoteRequestBinding request)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw InvalidToken("Checkout quote token is required when supplied.");
        }

        var segments = token.Split('.', StringSplitOptions.None);
        if (segments.Length != 2)
        {
            throw InvalidToken("Checkout quote token has an invalid format.");
        }

        try
        {
            var payloadBytes = Base64UrlDecode(segments[0]);
            var suppliedSignature = Base64UrlDecode(segments[1]);
            var expectedSignature = Sign(payloadBytes);
            if (suppliedSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                throw InvalidToken("Checkout quote token signature is invalid.");
            }

            var payload = JsonSerializer.Deserialize<CheckoutQuoteTokenPayload>(payloadBytes, SerializerOptions)
                          ?? throw InvalidToken("Checkout quote token payload is invalid.");
            ValidatePayload(payload, request);
            return payload;
        }
        catch (CheckoutQuoteConflictException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw InvalidToken("Checkout quote token has an invalid format.");
        }
    }

    private void ValidatePayload(CheckoutQuoteTokenPayload payload, CheckoutQuoteRequestBinding request)
    {
        if (payload.Version != 1 || payload.Items is not { Count: > 0 } || string.IsNullOrWhiteSpace(payload.Currency))
        {
            throw InvalidToken("Checkout quote token payload is invalid.");
        }

        if (payload.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new CheckoutQuoteConflictException("Checkout quote expired. Request a new quote before checkout.");
        }

        if (payload.CustomerId != request.CustomerId ||
            payload.BasketId != request.BasketId ||
            payload.BasketVersion != request.BasketVersion ||
            !string.Equals(payload.CouponCode, CheckoutRequestValidation.NormalizeCouponCode(request.CouponCode), StringComparison.Ordinal) ||
            payload.ShippingAddressId != request.ShippingAddressId)
        {
            throw new CheckoutQuoteConflictException("Checkout quote does not match the submitted checkout request.");
        }
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_signingKey, payload);

    private static CheckoutQuoteConflictException InvalidToken(string message) =>
        new(message, "CHECKOUT_QUOTE_INVALID");

    private static byte[] ResolveSigningKey(string? dedicatedSigningKey, string? jwtSigningKey)
    {
        if (!string.IsNullOrWhiteSpace(dedicatedSigningKey) &&
            !dedicatedSigningKey.Contains("SET_BY_ENVIRONMENT", StringComparison.OrdinalIgnoreCase) &&
            !dedicatedSigningKey.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetBytes(dedicatedSigningKey);
        }

        if (string.IsNullOrWhiteSpace(jwtSigningKey) ||
            jwtSigningKey.Contains("SET_BY_ENVIRONMENT", StringComparison.OrdinalIgnoreCase) ||
            jwtSigningKey.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CheckoutQuote:SigningKey or a valid Jwt:SecretKey is required.");
        }

        return HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtSigningKey),
            Encoding.UTF8.GetBytes("MicroShop.Ordering.CheckoutQuote.v1"));
    }

    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            0 => padded,
            2 => padded + "==",
            3 => padded + "=",
            _ => throw new FormatException("Invalid base64url value.")
        };
        return Convert.FromBase64String(padded);
    }
}
