using MediatR;

namespace IdentityService.Application.Auth;

public sealed record RegisterCommand(string UserName, string Password) : IRequest<RegisterResult>;
