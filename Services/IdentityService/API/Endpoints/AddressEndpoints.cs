using System.Security.Claims;
using IdentityService.Application.Addresses;

namespace IdentityService.API.Endpoints;
public static class AddressEndpoints
{
    public static IEndpointRouteBuilder MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/addresses").WithTags("Addresses").RequireAuthorization();
        group.MapGet("", async (ClaimsPrincipal user, AddressService service, CancellationToken ct) => TryCustomer(user, out var id) ? Results.Ok((await service.GetAsync(id, ct)).Select(ToDto)) : Results.Forbid());
        group.MapPost("", async (AddressRequest request, HttpRequest http, ClaimsPrincipal user, AddressService service, CancellationToken ct) => { if (!TryCustomer(user,out var id)) return Results.Forbid(); try { var address=await service.CreateAsync(id, ToInput(request), http.Headers["Idempotency-Key"].ToString(), ct); return Results.Created($"/me/addresses/{address.Id:D}", ToDto(address)); } catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string,string[]> { [ex.ParamName ?? "request"]=[ex.Message] }); } catch (InvalidOperationException ex) { return Results.Conflict(new { Message=ex.Message }); }});
        group.MapPatch("/{addressId:guid}", async (Guid addressId, AddressRequest request, ClaimsPrincipal user, AddressService service, CancellationToken ct) => { if (!TryCustomer(user,out var id)) return Results.Forbid(); try { var address=await service.UpdateAsync(id,addressId,ToInput(request),ct); return address is null ? Results.NotFound() : Results.Ok(ToDto(address)); } catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string,string[]> { [ex.ParamName ?? "request"]=[ex.Message] }); }});
        group.MapDelete("/{addressId:guid}", async (Guid addressId, ClaimsPrincipal user, AddressService service, CancellationToken ct) => !TryCustomer(user,out var id) ? Results.Forbid() : await service.ArchiveAsync(id,addressId,ct) ? Results.NoContent() : Results.NotFound());
        group.MapPut("/{addressId:guid}/default", async (Guid addressId, ClaimsPrincipal user, AddressService service, CancellationToken ct) => !TryCustomer(user,out var id) ? Results.Forbid() : await service.SetDefaultAsync(id,addressId,ct) ? Results.NoContent() : Results.NotFound());
        return app;
    }
    private static bool TryCustomer(ClaimsPrincipal user, out Guid id) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id);
    private static AddressInput ToInput(AddressRequest x) => new(x.Label,x.RecipientName,x.Line1,x.Line2,x.City,x.CountryCode,x.PostalCode,x.MakeDefault);
    private static object ToDto(IdentityService.Domain.Addresses.CustomerAddress x) => new { x.Id,x.Label,x.RecipientName,x.Line1,x.Line2,x.City,x.CountryCode,x.PostalCode,x.IsDefault,x.IsArchived,x.CreatedAtUtc,x.UpdatedAtUtc };
    private sealed record AddressRequest(string Label,string RecipientName,string Line1,string? Line2,string City,string CountryCode,string? PostalCode,bool MakeDefault);
}
