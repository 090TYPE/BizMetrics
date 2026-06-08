using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
            e.Property(o => o.SubscriptionStatus).HasConversion<string>();
            e.HasOne(o => o.Plan).WithMany().HasForeignKey(o => o.PlanId);
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

        b.Entity<Dataset>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(200).IsRequired();
            e.Property(d => d.Status).HasConversion<string>();
            e.HasIndex(d => d.OrganizationId);
        });

        // --- Multitenancy guardrail ---
        // Every entity implementing ITenantEntity is filtered by the current
        // tenant. With this in place a developer physically cannot query another
        // org's rows by forgetting a WHERE clause.
        b.Entity<Dataset>().HasQueryFilter(d => d.OrganizationId == _tenant.OrganizationId);
    }
}
