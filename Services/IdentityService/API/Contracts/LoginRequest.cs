namespace IdentityService.API.Contracts;

public sealed record LoginRequest(
    string UserName,
    string Password);
