using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentityService.Infrastructure.Bootstrap;

namespace IdentityService.Infrastructure.Persistence;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();

        await initializer.InitializeAsync(cancellationToken);
        await bootstrapper.BootstrapAsync(cancellationToken);
    }
}
