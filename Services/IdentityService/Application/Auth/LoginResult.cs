namespace IdentityService.Application.Auth;

public sealed record LoginResult(
    string AccessToken,
    DateTime ExpiresAt,
    string TokenType = "Bearer");