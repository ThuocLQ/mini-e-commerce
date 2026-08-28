namespace IdentityService.API.Contracts;

public sealed record RegisterRequest(string UserName, string Email, string Password);