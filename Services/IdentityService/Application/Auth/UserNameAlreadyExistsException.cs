namespace IdentityService.Application.Auth;

public sealed class UserNameAlreadyExistsException(string userName)
    : Exception($"Username '{userName}' is already registered.");
