using IdentityService.Application.Auth;
using IdentityService.Domain.Users;

namespace IdentityService.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> CreateAsync(AppUser user, CancellationToken cancellationToken = default);
    Task<bool> CreateWithEmailVerificationAsync(AppUser user, byte[] tokenHash, DateTime expiresAtUtc, string eventContent, string? correlationId, CancellationToken cancellationToken = default);
    Task<EmailVerificationIssueResult> IssueEmailVerificationAsync(Guid userId, byte[] tokenHash, DateTime expiresAtUtc, string eventContent, string? correlationId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> VerifyEmailAsync(byte[] tokenHash, DateTime nowUtc, CancellationToken cancellationToken = default);
}
