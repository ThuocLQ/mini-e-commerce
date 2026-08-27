using IdentityService.Application.Addresses;

namespace IdentityService.API.Endpoints;

public static class InternalAddressEndpoints
{
    public static IEndpointRouteBuilder MapInternalAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var configuration = app.ServiceProvider.GetRequiredService<IConfiguration>();
        var group = app.MapGroup("/internal/customers")
            .WithTags("Internal Addresses")
            .ExcludeFromDescription()
            .RequireInternalApiKey(configuration);

        group.MapGet("/{customerId:guid}/addresses/{addressId:guid}", async (
            Guid customerId,
            Guid addressId,
            AddressService service,
            CancellationToken cancellationToken) =>
        {
            var address = await service.GetAsync(customerId, addressId, cancellationToken);
            return address is null || address.IsArchived
                ? Results.NotFound()
                : Results.Ok(new AddressSnapshotResponse(
                    address.Id,
                    address.Label,
                    address.RecipientName,
                    address.Line1,
                    address.Line2,
                    address.City,
                    address.CountryCode,
                    address.PostalCode));
        });

        return app;
    }

    private sealed record AddressSnapshotResponse(
        Guid AddressId,
        string Label,
        string RecipientName,
        string Line1,
        string? Line2,
        string City,
        string CountryCode,
        string? PostalCode);
}
