using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Infrastructure.Billing;

/// <summary>
/// Checks whether the current org's subscription allows a given action,
/// and whether the org is within its plan's usage limits.
/// </summary>
public class PlanGuard
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    // Conservative fallback for new trial orgs that don't have a plan yet.
    private static readonly Plan FallbackFreePlan = new()
    {
        Name = "Free",
        MaxUsers = 2,
        MaxDatasets = 3,
        MaxRows = 10_000,
        PriceMonthly = 0
    };

    public PlanGuard(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // ── Dataset upload ─────────────────────────────────────────────────────

    public async Task<(bool Allowed, string? Reason)> CanUploadDatasetAsync()
    {
        var (org, plan) = await LoadAsync();
        if (org is null) return (false, "Organization not found.");

        if (!IsActive(org))
            return (false, BlockReason(org));

        var count = await _db.Datasets.CountAsync();  // global filter → current org only
        if (count >= plan.MaxDatasets)
            return (false, $"Dataset limit reached ({plan.MaxDatasets} on {plan.Name} plan). " +
                           "Upgrade to upload more.");

        return (true, null);
    }

    // ── Member invite ──────────────────────────────────────────────────────

    public async Task<(bool Allowed, string? Reason)> CanInviteMemberAsync()
    {
        var (org, plan) = await LoadAsync();
        if (org is null) return (false, "Organization not found.");

        if (!IsActive(org))
            return (false, BlockReason(org));

        var orgId = _tenant.OrganizationId;
        var count = await _db.Memberships
            .CountAsync(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active);

        if (count >= plan.MaxUsers)
            return (false, $"Member limit reached ({plan.MaxUsers} on {plan.Name} plan). " +
                           "Upgrade to invite more.");

        return (true, null);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<(Organization? org, Plan plan)> LoadAsync()
    {
        var orgId = _tenant.OrganizationId;
        if (orgId is null) return (null, FallbackFreePlan);

        var org = await _db.Organizations
            .Include(o => o.Plan)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        return (org, org?.Plan ?? FallbackFreePlan);
    }

    public static bool IsActive(Organization org) =>
        org.SubscriptionStatus == SubscriptionStatus.Active ||
        (org.SubscriptionStatus == SubscriptionStatus.Trialing &&
         (org.TrialEndsAt is null || org.TrialEndsAt > DateTime.UtcNow));

    private static string BlockReason(Organization org) =>
        org.SubscriptionStatus switch
        {
            SubscriptionStatus.Trialing =>
                "Your trial has expired. Please subscribe to continue.",
            SubscriptionStatus.PastDue =>
                "Your subscription is past due. Please update your payment method.",
            SubscriptionStatus.Canceled =>
                "Your subscription has been canceled. Please re-subscribe to continue.",
            _ => "Your subscription is not active."
        };
}
