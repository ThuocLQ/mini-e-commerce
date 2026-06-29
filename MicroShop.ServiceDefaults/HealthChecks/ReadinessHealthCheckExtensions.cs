using System.Data.Common;
using MicroShop.ServiceDefaults.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ReadinessHealthCheckExtensions
{
    private static readonly string[] ReadyTags = ["ready"];

    public static IServiceCollection AddTcpReadinessCheck(
        this IServiceCollection services,
        string name,
        string host,
        int port)
    {
        services.AddHealthChecks()
            .AddCheck(
                name,
                new TcpReadinessHealthCheck(host, port),
                tags: ReadyTags);

        return services;
    }

    public static IServiceCollection AddPostgresReadinessCheck(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName,
        string name = "postgres")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
                               ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' is missing.");

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        var host = GetConnectionStringValue(builder, "Host", "Server")
                   ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' is missing Host.");

        var portValue = GetConnectionStringValue(builder, "Port");
        var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 5432;

        return services.AddTcpReadinessCheck(name, host, port);
    }

    public static IServiceCollection AddRedisReadinessCheck(
        this IServiceCollection services,
        string connectionString,
        string name = "redis")
    {
        var (host, port) = ParseEndpoint(connectionString, defaultPort: 6379);

        return services.AddTcpReadinessCheck(name, host, port);
    }

    public static IServiceCollection AddRabbitMqReadinessCheck(
        this IServiceCollection services,
        IConfiguration configuration,
        string name = "rabbitmq")
    {
        var host = configuration["RabbitMq:Host"]
                   ?? throw new InvalidOperationException("RabbitMq:Host is missing.");

        var port = int.TryParse(configuration["RabbitMq:Port"], out var parsedPort)
            ? parsedPort
            : 5672;

        return services.AddTcpReadinessCheck(name, host, port);
    }

    private static string? GetConnectionStringValue(
        DbConnectionStringBuilder builder,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static (string Host, int Port) ParseEndpoint(string connectionString, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string is missing.");
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            var uriPort = uri.Port > 0 ? uri.Port : defaultPort;
            return (uri.Host, uriPort);
        }

        var endpoint = connectionString.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
        var atIndex = endpoint.LastIndexOf('@');
        if (atIndex >= 0)
        {
            endpoint = endpoint[(atIndex + 1)..];
        }

        var colonIndex = endpoint.LastIndexOf(':');
        if (colonIndex > 0 && int.TryParse(endpoint[(colonIndex + 1)..], out var parsedPort))
        {
            return (endpoint[..colonIndex], parsedPort);
        }

        return (endpoint, defaultPort);
    }
}
