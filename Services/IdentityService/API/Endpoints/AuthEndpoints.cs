using System.Security.Claims;
using IdentityService.API.Contracts;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.API.Endpoints;

public static class AuthEndpoints
{
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
            catch (UserNameAlreadyExistsException) { return Results.Conflict(new { Message = "Username is already registered." }); }
            catch (EmailAlreadyExistsException) { return Results.Conflict(new { Message = "Email is already registered." }); }
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

        group.MapGet("/me", [Authorize] async (ClaimsPrincipal principal, IUserRepository users, CancellationToken cancellationToken) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = principal.FindFirstValue(ClaimTypes.Name);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (!Guid.TryParse(userId, out var parsedUserId)) return Results.Unauthorized();

            var account = await users.GetByIdAsync(parsedUserId, cancellationToken);
            if (account is null || !account.IsActive) return Results.Unauthorized();

            return Results.Ok(new { UserId = userId, UserName = userName, Role = role, IsEmailVerified = account.IsEmailVerified });
        });

        return app;
    }
}
