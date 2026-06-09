namespace BizMetrics.Domain.Entities;

/// <summary>
/// Idempotency log for processed Stripe webhook events.
/// Prevents double-processing when Stripe retries delivery.
/// </summary>
public class StripeEventLog
{
    /// <summary>Stripe event ID (evt_...). Used as the primary key.</summary>
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
