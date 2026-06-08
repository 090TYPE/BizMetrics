namespace BizMetrics.Domain.Entities;

/// <summary>
/// Role of a member within a single organization. Ordered from most to least
/// privileged; authorization policies compare against these.
/// </summary>
public enum OrgRole
{
    Owner = 0,
    Admin = 1,
    Member = 2,
    Viewer = 3
}

public enum MembershipStatus
{
    Active,
    Invited,
    Suspended
}

/// <summary>Join entity linking a <see cref="User"/> to an <see cref="Organization"/> with a role.</summary>
public class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public OrgRole Role { get; set; } = OrgRole.Member;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
