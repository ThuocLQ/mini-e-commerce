using System.Security.Cryptography;
using System.Text;

namespace DiscountService.API.Endpoints;

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
            var supplied = Encoding.UTF8.GetBytes(suppliedKey);
            var expected = Encoding.UTF8.GetBytes(expectedKey);
            var isValid = supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
            if (!isValid)
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(InternalApiEndpointSecurity));
                logger.LogWarning(
                    "Rejected internal discount request. HeaderPresent={HeaderPresent}; SuppliedLength={SuppliedLength}; ExpectedLength={ExpectedLength}",
                    !string.IsNullOrWhiteSpace(suppliedKey),
                    supplied.Length,
                    expected.Length);
                return Results.NotFound();
            }

            return await next(context);
        });

        return group;
    }
}
