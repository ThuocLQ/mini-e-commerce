namespace IdentityService.Domain.Users;

public class AppUser
{
    public Guid Id { get; }
    public string UserName { get; }
    public string PasswordHash { get; }
    public string Role { get; }
    public bool IsActive { get; }
    public string? Email { get; }
    public bool IsEmailVerified { get; }
    public int SessionVersion { get; }
    public bool ReceivesOrderUpdates { get; }

    public AppUser(Guid id, string userName, string passwordHash, string role, bool isActive, string? email = null, bool isEmailVerified = false, int sessionVersion = 1, bool receivesOrderUpdates = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("User id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Username is required.", nameof(userName));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
        if (sessionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(sessionVersion), "Session version must be positive.");

        Id = id;
        UserName = userName.Trim();
        PasswordHash = passwordHash;
        Role = role.Trim();
        IsActive = isActive;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        IsEmailVerified = Email is not null && isEmailVerified;
        SessionVersion = sessionVersion;
        ReceivesOrderUpdates = receivesOrderUpdates;
    }
}
