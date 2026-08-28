namespace IdentityService.Application.Auth;

public sealed class EmailAlreadyExistsException(string email)
    : Exception($"Email '{email}' is already registered.");