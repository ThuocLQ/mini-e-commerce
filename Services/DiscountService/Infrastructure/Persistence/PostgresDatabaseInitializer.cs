using System.Reflection;
using DbUp;

namespace DiscountService.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private readonly string _connectionString;

    public PostgresDatabaseInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DiscountDb")
            ?? throw new InvalidOperationException("Connection string 'DiscountDb' is missing.");
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
            throw new InvalidOperationException("Discount database migration failed.", result.Error);
        }

        return Task.CompletedTask;
    }
}
