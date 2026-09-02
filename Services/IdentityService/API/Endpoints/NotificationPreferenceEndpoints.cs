using System.Security.Claims;
using IdentityService.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.API.Endpoints;

public static class NotificationPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/notification-preferences")
            .WithTags("Notification Preferences")
            .RequireAuthorization();

        group.MapGet("", async (ClaimsPrincipal principal, IUserRepository users, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await users.GetByIdAsync(userId, cancellationToken);
            return user is null || !user.IsActive
                ? Results.Unauthorized()
                : Results.Ok(new NotificationPreferenceResponse(user.ReceivesOrderUpdates));
        });

        group.MapPut("", async (
            NotificationPreferenceRequest request,
            ClaimsPrincipal principal,
            IUserRepository users,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var updated = await users.UpdateOrderNotificationPreferenceAsync(
                userId,
                request.ReceiveOrderUpdates,
                cancellationToken);

            return updated
                ? Results.Ok(new NotificationPreferenceResponse(request.ReceiveOrderUpdates))
                : Results.Unauthorized();
        });

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub"),
            out userId);

    private sealed record NotificationPreferenceRequest(bool ReceiveOrderUpdates);
    private sealed record NotificationPreferenceResponse(bool ReceiveOrderUpdates);
}