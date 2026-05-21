using System.Reflection;
using DbUp;

namespace IdentityService.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private readonly string _connectionString;

    public PostgresDatabaseInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' is missing.");
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
            throw new InvalidOperationException("Identity database migration failed.", result.Error);
        }

        return Task.CompletedTask;
    }
}
