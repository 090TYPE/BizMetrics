namespace BizMetrics.Domain.Entities;

/// <summary>
/// A global identity. A single user can belong to many organizations through
/// <see cref="Membership"/>; the user record itself carries no tenant scope.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
