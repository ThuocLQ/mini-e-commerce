namespace ApiGateway;

public static class SecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("Referrer-Policy", "no-referrer");
                headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

                return Task.CompletedTask;
            });

            await next();
        });
    }

    public static IApplicationBuilder UseDebugRouteGuard(
        this IApplicationBuilder app,
        GatewayOptions options)
    {
        return app.Use(async (context, next) =>
        {
            if (options.BlockDebugRoutesOutsideDevelopment &&
                !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
                context.Request.Path.StartsWithSegments("/debug"))
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not found",
                        type: "https://microshop.dev/problems/debug-route-not-available",
                        detail: "Debug routes are not available in this environment.")
                    .ExecuteAsync(context);
                return;
            }

            if (options.BlockInternalRoutesOutsideDevelopment &&
                !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
                IsInternalRoute(context.Request.Path))
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not found",
                        type: "https://microshop.dev/problems/internal-route-not-available",
                        detail: "Internal routes are not available through the public gateway in this environment.")
                    .ExecuteAsync(context);
                return;
            }

            await next();
        });
    }

    private static bool IsInternalRoute(PathString path)
    {
        var value = path.Value;

        return value?.EndsWith("/payment-result", StringComparison.OrdinalIgnoreCase) == true
               || value?.EndsWith("/payment-events", StringComparison.OrdinalIgnoreCase) == true;
    }
}
