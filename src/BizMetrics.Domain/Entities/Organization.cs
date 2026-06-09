namespace BizMetrics.Domain.Entities;

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled
}

/// <summary>
/// A tenant. Every tenant-scoped entity carries this organization's id and is
/// transparently filtered by it (see the EF Core global query filter).
/// </summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly unique handle, e.g. "acme-co".</summary>
    public string Slug { get; set; } = string.Empty;

    // Billing
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trialing;
    public DateTime? TrialEndsAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
