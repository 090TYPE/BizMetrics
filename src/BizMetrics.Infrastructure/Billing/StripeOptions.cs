namespace BizMetrics.Infrastructure.Billing;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>Stripe Price IDs — create them in your Stripe dashboard and set via env vars.</summary>
    public string ProPriceId { get; init; } = string.Empty;
    public string BusinessPriceId { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
