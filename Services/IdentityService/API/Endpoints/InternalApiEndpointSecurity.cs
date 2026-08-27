using System.Security.Cryptography;
using System.Text;

namespace IdentityService.API.Endpoints;

internal static class InternalApiEndpointSecurity
{
    private const string HeaderName = "X-MicroShop-Internal-Key";

    public static RouteGroupBuilder RequireInternalApiKey(this RouteGroupBuilder group, IConfiguration configuration)
    {
        var expectedKey = configuration["InternalApi:Key"]
            ?? throw new InvalidOperationException("InternalApi:Key is missing.");

        group.AddEndpointFilter(async (context, next) =>
        {
            var suppliedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
            var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
            if (suppliedBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
            {
                return Results.NotFound();
            }

            return await next(context);
        });

        return group;
    }
}
