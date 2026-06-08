namespace BizMetrics.Domain.Entities;

/// <summary>Billing plan / tier. Seeded; mapped to a Stripe Price in Phase 5.</summary>
public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? StripePriceId { get; set; }

    public int MaxUsers { get; set; }
    public int MaxDatasets { get; set; }
    public long MaxRows { get; set; }
    public decimal PriceMonthly { get; set; }
}
