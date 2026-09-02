using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using MicroShop.ServiceDefaults.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ValidateProductionConfiguration();

        builder.ConfigureOpenTelemetry();
        builder.AddDevelopmentOpenApi();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        var dataProtectionKeysDirectory = builder.Configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysDirectory))
        {
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory));
        }
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

        var resilienceSettings = ReadHttpClientResilienceSettings(builder.Configuration);

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(
                    Math.Max(1, resilienceSettings.AttemptTimeoutSeconds));
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(
                    Math.Max(1, resilienceSettings.TotalRequestTimeoutSeconds));

                options.Retry.MaxRetryAttempts = Math.Max(1, resilienceSettings.RetryMaxAttempts);
                options.Retry.Delay = TimeSpan.FromMilliseconds(
                    Math.Max(1, resilienceSettings.RetryDelayMilliseconds));

                options.CircuitBreaker.FailureRatio = Math.Clamp(
                    resilienceSettings.CircuitBreakerFailureRatio,
                    0.01,
                    1.0);
                options.CircuitBreaker.MinimumThroughput = Math.Max(
                    2,
                    resilienceSettings.CircuitBreakerMinimumThroughput);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(
                    Math.Max(1, resilienceSettings.CircuitBreakerSamplingDurationSeconds));
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(
                    Math.Max(1, resilienceSettings.CircuitBreakerBreakDurationSeconds));
            });

            http.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    private static TBuilder AddDevelopmentOpenApi<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenApi();
        }

        return builder;
    }

    private static void ValidateProductionConfiguration<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (!ShouldValidateProductionConfiguration(builder))
        {
            return;
        }

        var missingKeys = new List<string>();
        var placeholderKeys = new List<string>();

        foreach (var key in builder.Configuration.GetSection("ServiceDefaults:RequiredConfigurationKeys").GetChildren())
        {
            var configurationKey = key.Value;

            if (string.IsNullOrWhiteSpace(configurationKey))
            {
                continue;
            }

            var value = builder.Configuration[configurationKey];

            if (string.IsNullOrWhiteSpace(value))
            {
                missingKeys.Add(configurationKey);
                continue;
            }

            if (value.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase))
            {
                placeholderKeys.Add(configurationKey);
            }
        }

        if (missingKeys.Count == 0 && placeholderKeys.Count == 0)
        {
            return;
        }

        var message = "Production configuration validation failed.";

        if (missingKeys.Count > 0)
        {
            message += $" Missing keys: {string.Join(", ", missingKeys)}.";
        }

        if (placeholderKeys.Count > 0)
        {
            message += $" Placeholder keys: {string.Join(", ", placeholderKeys)}.";
        }

        throw new InvalidOperationException(message);
    }

    private static bool ShouldValidateProductionConfiguration<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (bool.TryParse(builder.Configuration["ServiceDefaults:ValidateProductionConfiguration"], out var enabled) && enabled)
        {
            return true;
        }

        return builder.Environment.IsProduction() ||
               builder.Environment.IsEnvironment("Kubernetes");
    }

    private static HttpClientResilienceSettings ReadHttpClientResilienceSettings(IConfiguration configuration)
    {
        var defaults = new HttpClientResilienceSettings();
        var sectionName = HttpClientResilienceSettings.SectionName;

        return new HttpClientResilienceSettings
        {
            AttemptTimeoutSeconds = ReadInt(configuration, sectionName, nameof(defaults.AttemptTimeoutSeconds), defaults.AttemptTimeoutSeconds),
            TotalRequestTimeoutSeconds = ReadInt(configuration, sectionName, nameof(defaults.TotalRequestTimeoutSeconds), defaults.TotalRequestTimeoutSeconds),
            RetryMaxAttempts = ReadInt(configuration, sectionName, nameof(defaults.RetryMaxAttempts), defaults.RetryMaxAttempts),
            RetryDelayMilliseconds = ReadInt(configuration, sectionName, nameof(defaults.RetryDelayMilliseconds), defaults.RetryDelayMilliseconds),
            CircuitBreakerFailureRatio = ReadDouble(configuration, sectionName, nameof(defaults.CircuitBreakerFailureRatio), defaults.CircuitBreakerFailureRatio),
            CircuitBreakerMinimumThroughput = ReadInt(configuration, sectionName, nameof(defaults.CircuitBreakerMinimumThroughput), defaults.CircuitBreakerMinimumThroughput),
            CircuitBreakerSamplingDurationSeconds = ReadInt(configuration, sectionName, nameof(defaults.CircuitBreakerSamplingDurationSeconds), defaults.CircuitBreakerSamplingDurationSeconds),
            CircuitBreakerBreakDurationSeconds = ReadInt(configuration, sectionName, nameof(defaults.CircuitBreakerBreakDurationSeconds), defaults.CircuitBreakerBreakDurationSeconds)
        };
    }

    private static int ReadInt(
        IConfiguration configuration,
        string sectionName,
        string key,
        int defaultValue)
    {
        return int.TryParse(configuration[$"{sectionName}:{key}"], out var value)
            ? value
            : defaultValue;
    }

    private static double ReadDouble(
        IConfiguration configuration,
        string sectionName,
        string key,
        double defaultValue)
    {
        return double.TryParse(configuration[$"{sectionName}:{key}"], out var value)
            ? value
            : defaultValue;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(MicroShopMetrics.MeterName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication UseStandardStatusCodeProblemDetails(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;
            if (response.StatusCode < StatusCodes.Status400BadRequest || response.HasStarted)
            {
                return;
            }

            var statusCode = response.StatusCode;
            var detail = statusCode switch
            {
                StatusCodes.Status401Unauthorized => "Authentication is required.",
                StatusCodes.Status403Forbidden => "You are not allowed to perform this action.",
                StatusCodes.Status404NotFound => "The requested resource was not found.",
                _ => "The request could not be completed."
            };

            await Results.Problem(
                    statusCode: statusCode,
                    title: ReasonPhrases.GetReasonPhrase(statusCode),
                    type: $"https://microshop.dev/problems/http-{statusCode}",
                    detail: detail,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = $"HTTP_{statusCode}",
                        ["message"] = detail,
                        ["error"] = detail,
                        ["correlationId"] = statusCodeContext.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? statusCodeContext.HttpContext.TraceIdentifier
                    })
                .ExecuteAsync(statusCodeContext.HttpContext);
        });

        return app;
    }
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Exposing health endpoints outside local/dev should be a conscious deployment choice.
        // Docker local-prod keeps service health endpoints on the private Compose network.
        app.UseStandardStatusCodeProblemDetails();

        if (ShouldMapHealthEndpoints(app))
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }

    private static bool ShouldMapHealthEndpoints(WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
        {
            return true;
        }

        return bool.TryParse(app.Configuration["ServiceDefaults:ExposeHealthEndpoints"], out var exposeHealthEndpoints)
            && exposeHealthEndpoints;
    }

    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        return app;
    }
}
