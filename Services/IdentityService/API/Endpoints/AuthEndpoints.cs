using System.Security.Claims;
using IdentityService.API.Contracts;
using IdentityService.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new LoginCommand(request.UserName, request.Password);
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? Results.Unauthorized()
                : Results.Ok(result);
        });

        group.MapGet("/me", [Authorize] (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = user.FindFirstValue(ClaimTypes.Name);
            var role = user.FindFirstValue(ClaimTypes.Role);

            return Results.Ok(new
            {
                UserId = userId,
                UserName = userName,
                Role = role
            });
        });

        return app;
    }
}
