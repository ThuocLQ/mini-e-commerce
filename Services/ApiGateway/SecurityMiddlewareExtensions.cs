using System.Net.Mime;

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
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = MediaTypeNames.Application.Json;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Debug routes are not available in this environment."
                });
                return;
            }

            await next();
        });
    }
}
