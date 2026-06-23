using System.Threading.RateLimiting;
using ApiGateway;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();

var gatewayOptions = builder.Configuration
    .GetSection(GatewayOptions.SectionName)
    .Get<GatewayOptions>()
    ?? new GatewayOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCors", policy =>
    {
        if (gatewayOptions.AllowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(gatewayOptions.AllowedCorsOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var category = GetRateLimitCategory(context.Request.Path);
        var clientKey = GetClientKey(context);
        var permitLimit = category switch
        {
            "webhook" => Math.Max(1, gatewayOptions.WebhookPermitLimit),
            "health" => Math.Max(1, gatewayOptions.HealthPermitLimit),
            _ => Math.Max(1, gatewayOptions.GeneralPermitLimit)
        };

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{category}:{clientKey}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(Math.Max(1, gatewayOptions.WindowSeconds)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCorrelationId();
app.UseSecurityHeaders();
app.UseDebugRouteGuard(gatewayOptions);
app.UseCors("GatewayCors");
app.UseRateLimiter();

app.MapDefaultEndpoints();
if (!app.Environment.IsDevelopment())
{
    app.MapHealthChecks("/health");
}

app.MapGet("/", () => Results.Ok(new
{
    service = "ApiGateway",
    version = "v1",
    status = "running"
}));

app.MapReverseProxy();

app.Run();

static string GetRateLimitCategory(PathString path)
{
    if (path.StartsWithSegments("/webhooks"))
    {
        return "webhook";
    }

    if (path.StartsWithSegments("/health") || path.StartsWithSegments("/alive"))
    {
        return "health";
    }

    return "general";
}

static string GetClientKey(HttpContext context)
{
    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
    {
        var firstForwardedIp = forwardedFor.ToString().Split(',', StringSplitOptions.TrimEntries)[0];
        if (!string.IsNullOrWhiteSpace(firstForwardedIp))
        {
            return firstForwardedIp;
        }
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
