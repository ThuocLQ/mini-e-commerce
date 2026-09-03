using System.Reflection;
using DbUp;

namespace NotificationWorker.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private readonly string _connectionString;

    public PostgresDatabaseInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationDb' is missing.");
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var result = DeployChanges
            .To
            .PostgresqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                script => script.Contains(".Infrastructure.Persistence.Migrations."))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("Notification database migration failed.", result.Error);
        }

        return Task.CompletedTask;
    }
}