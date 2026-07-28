namespace IdentityService.Infrastructure.Bootstrap;

public interface IAdminBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
