using System.ComponentModel.DataAnnotations;

namespace BizMetrics.Api.Contracts;

public record BillingStatusResponse(
    string PlanName,
    decimal PriceMonthly,
    string SubscriptionStatus,
    DateTime? TrialEndsAt,
    int? TrialDaysLeft,
    bool HasStripeCustomer,
    bool StripeConfigured,
    BillingUsage Usage
);

public record BillingUsage(
    int DatasetsUsed,
    int DatasetsMax,
    int MembersUsed,
    int MembersMax
);

public record CheckoutRequest(
    [Required] string Plan,
    [Required] string SuccessUrl,
    [Required] string CancelUrl
);

public record PortalRequest(
    [Required] string ReturnUrl
);
