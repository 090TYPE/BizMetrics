using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using CheckoutSession = Stripe.Checkout.Session;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using PortalSession = Stripe.BillingPortal.Session;
using PortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using PortalSessionService = Stripe.BillingPortal.SessionService;

namespace BizMetrics.Infrastructure.Billing;

/// <summary>
/// Wraps all Stripe interactions: checkout sessions, customer portal,
/// and idempotent webhook event processing.
/// </summary>
public class BillingService
{
    private readonly AppDbContext _db;
    private readonly StripeOptions _opts;

    public BillingService(AppDbContext db, IOptions<StripeOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    // ── Checkout ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Stripe Checkout session for the given plan and returns the redirect URL.
    /// Stores/reuses the Stripe customer for this org.
    /// </summary>
    public async Task<string> CreateCheckoutSessionAsync(
        Guid orgId, string planName,
        string userEmail,
        string successUrl, string cancelUrl)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _opts.SecretKey;

        var priceId = planName.ToLowerInvariant() switch
        {
            "pro" => _opts.ProPriceId,
            "business" => _opts.BusinessPriceId,
            _ => throw new ArgumentException($"Unknown plan: {planName}")
        };

        if (string.IsNullOrWhiteSpace(priceId))
            throw new InvalidOperationException(
                $"Stripe price ID for '{planName}' is not configured. " +
                "Set Stripe__ProPriceId / Stripe__BusinessPriceId.");

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId)
            ?? throw new InvalidOperationException("Organization not found.");

        var customerId = await GetOrCreateCustomerAsync(org, userEmail);

        var svc = new CheckoutSessionService();
        var session = await svc.CreateAsync(new CheckoutSessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = [new CheckoutSessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            // Store org_id + plan_name in metadata for the webhook handler
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["org_id"] = orgId.ToString(),
                    ["plan_name"] = planName.ToLowerInvariant()
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["org_id"] = orgId.ToString(),
                ["plan_name"] = planName.ToLowerInvariant()
            }
        });

        return session.Url;
    }

    // ── Customer portal ────────────────────────────────────────────────────

    /// <summary>Creates a Stripe Customer Portal session and returns the redirect URL.</summary>
    public async Task<string> CreatePortalSessionAsync(Guid orgId, string returnUrl)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _opts.SecretKey;

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId)
            ?? throw new InvalidOperationException("Organization not found.");

        if (string.IsNullOrWhiteSpace(org.StripeCustomerId))
            throw new InvalidOperationException(
                "No Stripe customer exists for this organization. " +
                "Please start a subscription first.");

        var svc = new PortalSessionService();
        var session = await svc.CreateAsync(new PortalSessionCreateOptions
        {
            Customer = org.StripeCustomerId,
            ReturnUrl = returnUrl
        });

        return session.Url;
    }

    // ── Webhooks ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies a Stripe webhook, processes the event, and records it for idempotency.
    /// Returns false if the event was already processed; throws on signature failure.
    /// </summary>
    public async Task<bool> HandleWebhookAsync(string rawBody, string signature)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _opts.SecretKey;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(rawBody, signature, _opts.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new InvalidOperationException(
                $"Stripe webhook signature verification failed: {ex.Message}", ex);
        }

        // Idempotency guard — skip events we've already handled
        if (await _db.StripeEventLogs.AnyAsync(e => e.EventId == stripeEvent.Id))
            return false;

        await DispatchAsync(stripeEvent);

        _db.StripeEventLogs.Add(new StripeEventLog
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ProcessedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Event dispatching ──────────────────────────────────────────────────

    private async Task DispatchAsync(Event evt)
    {
        switch (evt.Type)
        {
            case "checkout.session.completed":
                await OnCheckoutCompletedAsync((CheckoutSession)evt.Data.Object);
                break;
            case "customer.subscription.updated":
                await OnSubscriptionUpdatedAsync((Subscription)evt.Data.Object);
                break;
            case "customer.subscription.deleted":
                await OnSubscriptionDeletedAsync((Subscription)evt.Data.Object);
                break;
            case "invoice.payment_failed":
                await OnInvoicePaymentFailedAsync((Invoice)evt.Data.Object);
                break;
            // All other events are acknowledged but ignored
        }
    }

    private async Task OnCheckoutCompletedAsync(CheckoutSession session)
    {
        if (!Guid.TryParse(session.Metadata?.GetValueOrDefault("org_id"), out var orgId)) return;

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId);
        if (org is null) return;

        org.StripeCustomerId = session.CustomerId;
        org.StripeSubscriptionId = session.SubscriptionId;
        org.SubscriptionStatus = SubscriptionStatus.Active;

        // Map session's plan_name metadata to a DB plan
        var planName = session.Metadata?.GetValueOrDefault("plan_name");
        if (planName is not null)
        {
            var plan = await _db.Plans.FirstOrDefaultAsync(
                p => p.Name.ToLower() == planName);
            if (plan is not null) org.PlanId = plan.Id;
        }
    }

    private async Task OnSubscriptionUpdatedAsync(Subscription subscription)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.StripeSubscriptionId == subscription.Id);
        if (org is null) return;

        org.SubscriptionStatus = MapStripeStatus(subscription.Status);

        // Try to find the plan by Stripe price ID (set if admin configured it in DB)
        var priceId = subscription.Items.Data.FirstOrDefault()?.Price.Id;
        if (priceId is not null)
        {
            var plan = await _db.Plans.FirstOrDefaultAsync(p => p.StripePriceId == priceId);
            if (plan is not null) org.PlanId = plan.Id;
        }
    }

    private async Task OnSubscriptionDeletedAsync(Subscription subscription)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.StripeSubscriptionId == subscription.Id);
        if (org is null) return;

        org.SubscriptionStatus = SubscriptionStatus.Canceled;
        org.StripeSubscriptionId = null;
    }

    private async Task OnInvoicePaymentFailedAsync(Invoice invoice)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.StripeCustomerId == invoice.CustomerId);
        if (org is null) return;

        org.SubscriptionStatus = SubscriptionStatus.PastDue;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<string> GetOrCreateCustomerAsync(Organization org, string email)
    {
        if (!string.IsNullOrWhiteSpace(org.StripeCustomerId))
            return org.StripeCustomerId;

        var svc = new CustomerService();
        var customer = await svc.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = org.Name,
            Metadata = new Dictionary<string, string> { ["org_id"] = org.Id.ToString() }
        });

        org.StripeCustomerId = customer.Id;
        await _db.SaveChangesAsync();

        return customer.Id;
    }

    private void EnsureConfigured()
    {
        if (!_opts.IsConfigured)
            throw new InvalidOperationException(
                "Stripe is not configured on this instance. " +
                "Set Stripe__SecretKey to enable billing.");
    }

    public static SubscriptionStatus MapStripeStatus(string stripeStatus) =>
        stripeStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" or "cancelled" or "unpaid" or "incomplete_expired" => SubscriptionStatus.Canceled,
            _ => SubscriptionStatus.Active
        };
}
