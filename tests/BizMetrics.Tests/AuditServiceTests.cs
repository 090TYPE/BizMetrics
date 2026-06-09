using BizMetrics.Domain.Audit;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Audit;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Tests;

/// <summary>Tests for the audit log service.</summary>
public class AuditServiceTests
{
    private static AppDbContext NewDb(string name, ITenantContext tenant)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts, tenant);
    }

    private static (AppDbContext db, TenantContext tenant, Guid orgId) Setup(string dbName)
    {
        var tenant = new TenantContext();
        var db = NewDb(dbName, tenant);
        var orgId = Guid.NewGuid();
        tenant.SetTenant(orgId);
        return (db, tenant, orgId);
    }

    // ── Basic log creation ─────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_creates_audit_entry()
    {
        var (db, tenant, orgId) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);
        var userId = Guid.NewGuid();

        await svc.LogAsync(userId, AuditActions.DatasetUploaded, "Dataset", "abc");

        var entry = await db.AuditEntries.SingleAsync();
        Assert.Equal(orgId, entry.OrganizationId);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(AuditActions.DatasetUploaded, entry.Action);
        Assert.Equal("Dataset", entry.EntityType);
        Assert.Equal("abc", entry.EntityId);
    }

    [Fact]
    public async Task LogAsync_serializes_details_to_json()
    {
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);

        await svc.LogAsync(null, AuditActions.OrgRenamed, "Organization",
            details: new { from = "Old", to = "New" });

        var entry = await db.AuditEntries.SingleAsync();
        Assert.NotNull(entry.Details);
        Assert.Contains("\"from\"", entry.Details);
        Assert.Contains("Old", entry.Details);
    }

    [Fact]
    public async Task LogAsync_stores_ip_address()
    {
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);

        await svc.LogAsync(null, AuditActions.UserLoggedIn, "User",
            ipAddress: "192.168.1.1");

        var entry = await db.AuditEntries.SingleAsync();
        Assert.Equal("192.168.1.1", entry.IpAddress);
    }

    // ── No tenant → no entry ───────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_silently_skips_when_no_tenant()
    {
        var tenant = new TenantContext(); // no tenant set
        var db = NewDb(Guid.NewGuid().ToString(), tenant);
        var svc = new AuditService(db, tenant);

        await svc.LogAsync(null, AuditActions.DatasetUploaded, "Dataset");

        Assert.Empty(await db.AuditEntries.ToListAsync());
    }

    // ── orgIdOverride ──────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_uses_orgIdOverride_when_provided()
    {
        // Set a different tenant than the override
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);
        var overrideOrgId = Guid.NewGuid();

        await svc.LogAsync(null, AuditActions.MemberJoined, "Membership",
            orgIdOverride: overrideOrgId);

        var entry = await db.AuditEntries.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(overrideOrgId, entry.OrganizationId);
    }

    // ── Multiple entries ───────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_multiple_calls_create_independent_entries()
    {
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);

        await svc.LogAsync(null, AuditActions.DatasetUploaded, "Dataset", "1");
        await svc.LogAsync(null, AuditActions.DatasetDeleted, "Dataset", "1");
        await svc.LogAsync(null, AuditActions.OrgRenamed, "Organization");

        Assert.Equal(3, await db.AuditEntries.CountAsync());
    }

    // ── CreatedAt is set ───────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_sets_created_at_close_to_now()
    {
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await svc.LogAsync(null, AuditActions.UserLoggedIn, "User");

        var entry = await db.AuditEntries.SingleAsync();
        Assert.InRange(entry.CreatedAt, before, DateTime.UtcNow.AddSeconds(5));
    }

    // ── Silent on DB error ─────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_does_not_throw_on_invalid_state()
    {
        // An audit call that can't save (disposed context) must not propagate.
        var (db, tenant, _) = Setup(Guid.NewGuid().ToString());
        var svc = new AuditService(db, tenant);
        await db.DisposeAsync();

        // Should complete without throwing
        await svc.LogAsync(null, AuditActions.DatasetUploaded, "Dataset");
    }

    // ── Action constants ───────────────────────────────────────────────────

    [Fact]
    public void AuditActions_constants_are_dot_namespaced()
    {
        var fields = typeof(AuditActions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(fields);
        Assert.All(fields, action => Assert.Contains(".", action));
    }
}
