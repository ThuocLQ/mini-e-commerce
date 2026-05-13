using MediatR;

namespace IdentityService.Application.Auth;

public sealed record LoginCommand(string UserName, string Password) : IRequest<LoginResult?>;