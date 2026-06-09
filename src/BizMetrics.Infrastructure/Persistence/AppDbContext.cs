using System.Text.Json;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BizMetrics.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DataRow> DataRows => Set<DataRow>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<Widget> Widgets => Set<Widget>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<StripeEventLog> StripeEventLogs => Set<StripeEventLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(200);
        });

        b.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
            e.Property(o => o.Name).HasMaxLength(200).IsRequired();
            e.Property(o => o.Slug).HasMaxLength(100).IsRequired();
            e.Property(o => o.StripeCustomerId).HasMaxLength(200);
            e.Property(o => o.StripeSubscriptionId).HasMaxLength(200);
            e.Property(o => o.SubscriptionStatus).HasConversion<string>();
            e.HasOne(o => o.Plan).WithMany().HasForeignKey(o => o.PlanId);
        });

        b.Entity<StripeEventLog>(e =>
        {
            e.HasKey(s => s.EventId);
            e.Property(s => s.EventId).HasMaxLength(200);
            e.Property(s => s.EventType).HasMaxLength(100).IsRequired();
        });

        b.Entity<Membership>(e =>
        {
            // A user can hold at most one membership per organization.
            e.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
            e.Property(m => m.Role).HasConversion<string>();
            e.Property(m => m.Status).HasConversion<string>();
            e.HasOne(m => m.User).WithMany(u => u.Memberships).HasForeignKey(m => m.UserId);
            e.HasOne(m => m.Organization).WithMany(o => o.Memberships).HasForeignKey(m => m.OrganizationId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(r => r.TokenHash);
            e.Property(r => r.TokenHash).HasMaxLength(200).IsRequired();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId);
        });

        b.Entity<Invitation>(e =>
        {
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.Role).HasConversion<string>();
            e.Property(i => i.Status).HasConversion<string>();
            e.Property(i => i.TokenHash).HasMaxLength(200).IsRequired();
            e.HasIndex(i => i.TokenHash);
            e.HasIndex(i => new { i.OrganizationId, i.Email });
            e.HasOne(i => i.Organization).WithMany().HasForeignKey(i => i.OrganizationId);
        });

        var listComparer = new ValueComparer<List<string>>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());
        var dictComparer = new ValueComparer<Dictionary<string, string?>>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.Count == b.Count && !a.Except(b).Any()),
            v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value == null ? 0 : kv.Value.GetHashCode())),
            v => new Dictionary<string, string?>(v));

        b.Entity<Dataset>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(200).IsRequired();
            e.Property(d => d.Status).HasConversion<string>();
            e.Property(d => d.Columns)
                .HasConversion(JsonbConverter<List<string>>(), listComparer)
                .HasColumnType("jsonb");
            e.HasIndex(d => d.OrganizationId);
        });

        b.Entity<DataRow>(e =>
        {
            e.Property(d => d.Data)
                .HasConversion(JsonbConverter<Dictionary<string, string?>>(), dictComparer)
                .HasColumnType("jsonb");
            e.HasIndex(d => new { d.DatasetId, d.RowIndex });
            e.HasOne(d => d.Dataset).WithMany().HasForeignKey(d => d.DatasetId);
        });

        b.Entity<Dashboard>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(200).IsRequired();
            e.HasMany(d => d.Widgets).WithOne(w => w.Dashboard).HasForeignKey(w => w.DashboardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Widget>(e =>
        {
            e.Property(w => w.Title).HasMaxLength(200).IsRequired();
            e.Property(w => w.ChartType).HasMaxLength(20);
            e.Property(w => w.QueryJson).HasColumnType("jsonb");
            e.HasIndex(w => w.DashboardId);
        });

        // --- Multitenancy guardrail ---
        // Every entity implementing ITenantEntity is filtered by the current
        // tenant. With this in place a developer physically cannot query another
        // org's rows by forgetting a WHERE clause.
        b.Entity<Dataset>().HasQueryFilter(d => d.OrganizationId == _tenant.OrganizationId);
        b.Entity<DataRow>().HasQueryFilter(d => d.OrganizationId == _tenant.OrganizationId);
        b.Entity<Dashboard>().HasQueryFilter(d => d.OrganizationId == _tenant.OrganizationId);
        b.Entity<Widget>().HasQueryFilter(w => w.OrganizationId == _tenant.OrganizationId);
    }

    /// <summary>A value converter that (de)serializes <typeparamref name="T"/> to a JSONB column.</summary>
    private static ValueConverter<T, string> JsonbConverter<T>() => new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null)!);
}
