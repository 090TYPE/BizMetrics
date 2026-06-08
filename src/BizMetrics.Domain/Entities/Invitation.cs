namespace BizMetrics.Domain.Entities;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked
}

/// <summary>
/// An invitation for an email address to join an organization with a given role.
/// Only the token hash is stored; the raw token travels in the emailed accept link.
/// Not tenant-filtered — the accept flow looks it up by token, not by current org.
/// </summary>
public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public OrgRole Role { get; set; } = OrgRole.Member;

    public string TokenHash { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid InvitedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    public bool IsRedeemable => Status == InvitationStatus.Pending && DateTime.UtcNow < ExpiresAt;
}
