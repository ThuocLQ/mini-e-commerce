using System.Security.Cryptography;
using System.Text;

namespace OrderingService.API.Endpoints;

internal static class InternalApiEndpointSecurity
{
    private const string InternalApiKeyHeaderName = "X-MicroShop-Internal-Key";

    public static RouteGroupBuilder RequireInternalApiKey(
        this RouteGroupBuilder group,
        IConfiguration configuration)
    {
        var expectedKey = configuration["InternalApi:Key"]
            ?? throw new InvalidOperationException("InternalApi:Key is missing.");

        group.AddEndpointFilter(async (context, next) =>
        {
            var suppliedKey = context.HttpContext.Request.Headers[InternalApiKeyHeaderName].ToString();
            if (!IsValidKey(suppliedKey, expectedKey))
            {
                // Do not reveal whether an internal route exists to an untrusted caller.
                return Results.NotFound();
            }

            return await next(context);
        });

        return group;
    }

    private static bool IsValidKey(string suppliedKey, string expectedKey)
    {
        if (string.IsNullOrWhiteSpace(suppliedKey))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
