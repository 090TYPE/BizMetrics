using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Billing;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Tests;

/// <summary>
/// Tests for Phase 5 billing: PlanGuard subscription checks, plan-limit enforcement,
/// and Stripe event-log idempotency guarantee.
/// </summary>
public class BillingTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static AppDbContext NewDb(string name, ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options, tenant);
    }

    private static (AppDbContext db, TenantContext tenant, Organization org) SeedOrg(
        string dbName,
        SubscriptionStatus status,
        DateTime? trialEndsAt = null,
        int maxDatasets = 10,
        int maxUsers = 5,
        int currentDatasets = 0,
        int currentMembers = 1)
    {
        var tenant = new TenantContext();
        var db = NewDb(dbName, tenant);

        var plan = new Plan
        {
            Id = Guid.NewGuid(), Name = "TestPlan",
            MaxDatasets = maxDatasets, MaxUsers = maxUsers, MaxRows = 100_000, PriceMonthly = 0
        };

        var org = new Organization
        {
            Id = Guid.NewGuid(), Name = "Test Org", Slug = "test-org",
            SubscriptionStatus = status,
            TrialEndsAt = trialEndsAt,
            Plan = plan, PlanId = plan.Id
        };

        db.Plans.Add(plan);
        db.Organizations.Add(org);

        // Seed datasets (bypassing tenant filter with direct OrganizationId assignment)
        for (var i = 0; i < currentDatasets; i++)
            db.Datasets.Add(new Dataset { OrganizationId = org.Id, Name = $"ds-{i}" });

        // Seed memberships
        var user = new User { Id = Guid.NewGuid(), Email = "owner@test.com", FullName = "Owner" };
        db.Users.Add(user);
        for (var i = 0; i < currentMembers; i++)
        {
            var u = i == 0 ? user : new User { Email = $"member{i}@test.com", FullName = $"Member {i}" };
            if (i > 0) db.Users.Add(u);
            db.Memberships.Add(new Membership
            {
                UserId = u.Id, OrganizationId = org.Id,
                Role = i == 0 ? OrgRole.Owner : OrgRole.Member,
                Status = MembershipStatus.Active
            });
        }

        db.SaveChanges();
        tenant.SetTenant(org.Id);
        return (db, tenant, org);
    }

    // ── IsActive helper ────────────────────────────────────────────────────

    [Fact]
    public void Active_subscription_is_considered_active()
    {
        var org = new Organization { SubscriptionStatus = SubscriptionStatus.Active };
        Assert.True(PlanGuard.IsActive(org));
    }

    [Fact]
    public void Trialing_within_trial_period_is_active()
    {
        var org = new Organization
        {
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(5)
        };
        Assert.True(PlanGuard.IsActive(org));
    }

    [Fact]
    public void Trialing_with_no_end_date_is_active()
    {
        var org = new Organization
        {
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = null
        };
        Assert.True(PlanGuard.IsActive(org));
    }

    [Fact]
    public void Expired_trial_is_not_active()
    {
        var org = new Organization
        {
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(-1)
        };
        Assert.False(PlanGuard.IsActive(org));
    }

    [Fact]
    public void Canceled_subscription_is_not_active()
    {
        var org = new Organization { SubscriptionStatus = SubscriptionStatus.Canceled };
        Assert.False(PlanGuard.IsActive(org));
    }

    [Fact]
    public void PastDue_subscription_is_not_active()
    {
        var org = new Organization { SubscriptionStatus = SubscriptionStatus.PastDue };
        Assert.False(PlanGuard.IsActive(org));
    }

    // ── Dataset upload limits ──────────────────────────────────────────────

    [Fact]
    public async Task Active_org_below_limit_can_upload_dataset()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(),
            SubscriptionStatus.Active, maxDatasets: 10, currentDatasets: 5);
        var guard = new PlanGuard(db, tenant);

        var (allowed, reason) = await guard.CanUploadDatasetAsync();

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public async Task Active_org_at_dataset_limit_cannot_upload()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(),
            SubscriptionStatus.Active, maxDatasets: 3, currentDatasets: 3);
        var guard = new PlanGuard(db, tenant);

        var (allowed, reason) = await guard.CanUploadDatasetAsync();

        Assert.False(allowed);
        Assert.NotNull(reason);
        Assert.Contains("Dataset limit reached", reason);
    }

    [Fact]
    public async Task Expired_trial_cannot_upload_dataset()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(),
            SubscriptionStatus.Trialing,
            trialEndsAt: DateTime.UtcNow.AddDays(-1));
        var guard = new PlanGuard(db, tenant);

        var (allowed, reason) = await guard.CanUploadDatasetAsync();

        Assert.False(allowed);
        Assert.NotNull(reason);
        Assert.Contains("trial", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Canceled_subscription_cannot_upload_dataset()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(), SubscriptionStatus.Canceled);
        var guard = new PlanGuard(db, tenant);

        var (allowed, _) = await guard.CanUploadDatasetAsync();
        Assert.False(allowed);
    }

    // ── Member invite limits ───────────────────────────────────────────────

    [Fact]
    public async Task Active_org_below_member_limit_can_invite()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(),
            SubscriptionStatus.Active, maxUsers: 5, currentMembers: 2);
        var guard = new PlanGuard(db, tenant);

        var (allowed, reason) = await guard.CanInviteMemberAsync();

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public async Task Active_org_at_member_limit_cannot_invite()
    {
        var (db, tenant, _) = SeedOrg(Guid.NewGuid().ToString(),
            SubscriptionStatus.Active, maxUsers: 2, currentMembers: 2);
        var guard = new PlanGuard(db, tenant);

        var (allowed, reason) = await guard.CanInviteMemberAsync();

        Assert.False(allowed);
        Assert.NotNull(reason);
        Assert.Contains("Member limit reached", reason);
    }

    // ── Stripe event-log idempotency ───────────────────────────────────────

    [Fact]
    public async Task StripeEventLog_duplicate_event_id_is_detectable()
    {
        var db = NewDb(Guid.NewGuid().ToString(), new TenantContext());
        const string eventId = "evt_test_abc123";

        // Simulate BillingService recording the event after first processing
        db.StripeEventLogs.Add(new StripeEventLog
        {
            EventId = eventId,
            EventType = "checkout.session.completed",
            ProcessedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // BillingService checks this before processing
        var alreadyProcessed = await db.StripeEventLogs
            .AnyAsync(e => e.EventId == eventId);
        var newEventUnprocessed = await db.StripeEventLogs
            .AnyAsync(e => e.EventId == "evt_different_xyz");

        Assert.True(alreadyProcessed);
        Assert.False(newEventUnprocessed);
    }

    [Fact]
    public async Task StripeEventLog_different_event_ids_are_independent()
    {
        var db = NewDb(Guid.NewGuid().ToString(), new TenantContext());

        db.StripeEventLogs.Add(new StripeEventLog { EventId = "evt_1", EventType = "foo" });
        db.StripeEventLogs.Add(new StripeEventLog { EventId = "evt_2", EventType = "bar" });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.StripeEventLogs.CountAsync());
    }

    // ── Stripe status mapping ──────────────────────────────────────────────

    [Theory]
    [InlineData("active", SubscriptionStatus.Active)]
    [InlineData("trialing", SubscriptionStatus.Trialing)]
    [InlineData("past_due", SubscriptionStatus.PastDue)]
    [InlineData("canceled", SubscriptionStatus.Canceled)]
    [InlineData("cancelled", SubscriptionStatus.Canceled)]
    [InlineData("unpaid", SubscriptionStatus.Canceled)]
    [InlineData("incomplete_expired", SubscriptionStatus.Canceled)]
    [InlineData("unknown_future_status", SubscriptionStatus.Active)]
    public void MapStripeStatus_maps_correctly(string stripeStatus, SubscriptionStatus expected)
    {
        var result = BillingService.MapStripeStatus(stripeStatus);
        Assert.Equal(expected, result);
    }
}
