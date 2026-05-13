namespace IdentityService.Domain.Users;

public class AppUser
{
    public Guid Id { get; }
    public string UserName { get; }
    public string Role { get; }

    public AppUser(Guid id, string userName, string role)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("Username is required.", nameof(userName));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role is required.", nameof(role));
        }

        Id = id;
        UserName = userName;
        Role = role;
    }
}
