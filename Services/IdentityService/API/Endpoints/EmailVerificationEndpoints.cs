using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Auth;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.API.Endpoints;

public static class EmailVerificationEndpoints
{
    public static IEndpointRouteBuilder MapEmailVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/email-verifications", async (VerifyEmailRequest request, IUserRepository users, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 256)
                return Results.BadRequest(new { Message = "Verification token is invalid." });

            var verified = await users.VerifyEmailAsync(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)), DateTime.UtcNow, cancellationToken);
            return verified ? Results.Ok(new { Verified = true }) : Results.BadRequest(new { Message = "Verification token is invalid or expired." });
        }).WithTags("Auth");

        app.MapPost("/auth/email-verifications/resend", [Authorize] async (ClaimsPrincipal principal, EmailVerificationService verificationService, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();

            var outcome = await verificationService.ResendAsync(userId, cancellationToken);
            return outcome == EmailVerificationIssueResult.RateLimited
                ? Results.StatusCode(StatusCodes.Status429TooManyRequests)
                : Results.Accepted();
        }).WithTags("Auth");

        return app;
    }

    private sealed record VerifyEmailRequest(string Token);
}
