using IdentityService.Application.Abstractions;

namespace IdentityService.API.Endpoints;

public static class InternalCustomerContactEndpoints
{
    public static IEndpointRouteBuilder MapInternalCustomerContactEndpoints(this IEndpointRouteBuilder app)
    {
        var configuration = app.ServiceProvider.GetRequiredService<IConfiguration>();
        var group = app.MapGroup("/internal/customers")
            .WithTags("Internal Customer Contacts")
            .ExcludeFromDescription()
            .RequireInternalApiKey(configuration);

        group.MapGet("/{customerId:guid}/contact", async (Guid customerId, IUserRepository userRepository, CancellationToken cancellationToken) =>
        {
            var user = await userRepository.GetByIdAsync(customerId, cancellationToken);
            return user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)
                ? Results.NotFound()
                : Results.Ok(new CustomerContactResponse(user.Id, user.Email, user.IsEmailVerified));
        });

        return app;
    }

    private sealed record CustomerContactResponse(Guid CustomerId, string Email, bool IsEmailVerified);
}
