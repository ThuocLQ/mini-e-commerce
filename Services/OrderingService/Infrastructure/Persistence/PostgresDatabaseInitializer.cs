using System.Reflection;
using DbUp;

namespace OrderingService.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private readonly string _connectionString;

    public PostgresDatabaseInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrderingDb")
            ?? throw new InvalidOperationException("Connection string 'OrderingDb' is missing.");
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
            throw new InvalidOperationException("Ordering database migration failed.", result.Error);
        }

        return Task.CompletedTask;
    }
}
