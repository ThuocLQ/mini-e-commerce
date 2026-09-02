using System.Security.Claims;
using IdentityService.API.Contracts;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Auth;
using MediatR;
using MicroShop.ServiceDefaults.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.API.Endpoints;

public static class AuthEndpoints
{
    private const string SessionVersionClaim = "microshop_session_version";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sender.Send(new RegisterCommand(request.UserName, request.Email, request.Password), cancellationToken);
                return Results.Created($"/auth/users/{result.UserId:D}", result);
            }
            catch (UserNameAlreadyExistsException) { return ApiProblemResults.Conflict("Username is already registered.", "USERNAME_ALREADY_REGISTERED"); }
            catch (EmailAlreadyExistsException) { return ApiProblemResults.Conflict("Email is already registered.", "EMAIL_ALREADY_REGISTERED"); }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] });
            }
        });

        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new LoginCommand(request.UserName, request.Password), cancellationToken);
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        });

        group.MapPost("/logout", [Authorize] async (ClaimsPrincipal principal, IUserRepository users, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await users.RevokeSessionsAsync(userId, cancellationToken) ? Results.NoContent() : Results.Unauthorized();
        });

        group.MapGet("/me", [Authorize] async (ClaimsPrincipal principal, IUserRepository users, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

            var account = await users.GetByIdAsync(userId, cancellationToken);
            if (account is null || !account.IsActive || !HasCurrentSessionVersion(principal, account.SessionVersion))
            {
                return Results.Unauthorized();
            }

            var userName = principal.FindFirstValue(ClaimTypes.Name);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            return Results.Ok(new { UserId = userId, UserName = userName, Role = role, IsEmailVerified = account.IsEmailVerified, ReceiveOrderUpdates = account.ReceivesOrderUpdates });
        });

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static bool HasCurrentSessionVersion(ClaimsPrincipal principal, int currentVersion) =>
        int.TryParse(principal.FindFirstValue(SessionVersionClaim), out var tokenVersion)
        && tokenVersion == currentVersion;
}
