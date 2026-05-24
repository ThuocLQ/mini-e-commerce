using Microsoft.Extensions.Configuration;

namespace OrderingService.Infrastructure.Messaging;

public static class RabbitMqOptionsResolver
{
    public static RabbitMqOptions Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return FromConnectionString(connectionString);
        }

        return configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
    }

    private static RabbitMqOptions FromConnectionString(string connectionString)
    {
        var uri = new Uri(connectionString);
        var credentials = uri.UserInfo.Split(':', 2);
        var virtualHost = uri.AbsolutePath.Trim('/');

        return new RabbitMqOptions
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? (ushort)5672 : (ushort)uri.Port,
            VirtualHost = string.IsNullOrWhiteSpace(virtualHost) ? "/" : Uri.UnescapeDataString(virtualHost),
            UserName = credentials.Length > 0 ? Uri.UnescapeDataString(credentials[0]) : "guest",
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "guest"
        };
    }
}
