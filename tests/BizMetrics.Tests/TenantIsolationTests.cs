using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BizMetrics.Tests;

/// <summary>
/// The headline guarantee of the platform: a tenant can only ever see its own
/// rows. These exercise the EF Core global query filter directly.
/// </summary>
public class TenantIsolationTests
{
    private static AppDbContext NewContext(string dbName, ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options, tenant);
    }

    [Fact]
    public async Task Datasets_are_scoped_to_the_current_tenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        // Seed one dataset per org. Seeding context has no tenant set, but we
        // assign OrganizationId explicitly on each row.
        var seedTenant = new TenantContext();
        await using (var seed = NewContext(dbName, seedTenant))
        {
            seed.Datasets.Add(new Dataset { OrganizationId = orgA, Name = "A-sales" });
            seed.Datasets.Add(new Dataset { OrganizationId = orgB, Name = "B-sales" });
            await seed.SaveChangesAsync();
        }

        // Acting as org A, we must see only A's dataset — no WHERE clause written.
        var tenantA = new TenantContext();
        tenantA.SetTenant(orgA);
        await using (var ctx = NewContext(dbName, tenantA))
        {
            var visible = await ctx.Datasets.ToListAsync();
            Assert.Single(visible);
            Assert.Equal("A-sales", visible[0].Name);
        }
    }

    [Fact]
    public async Task Tenant_cannot_fetch_another_tenants_row_by_id()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var bRowId = Guid.NewGuid();

        await using (var seed = NewContext(dbName, new TenantContext()))
        {
            seed.Datasets.Add(new Dataset { Id = bRowId, OrganizationId = orgB, Name = "B-secret" });
            await seed.SaveChangesAsync();
        }

        var tenantA = new TenantContext();
        tenantA.SetTenant(orgA);
        await using (var ctx = NewContext(dbName, tenantA))
        {
            // Even an explicit id lookup is filtered — returns null for org A.
            var stolen = await ctx.Datasets.FirstOrDefaultAsync(d => d.Id == bRowId);
            Assert.Null(stolen);
        }
    }

    [Fact]
    public async Task No_tenant_set_means_no_rows()
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var seed = NewContext(dbName, new TenantContext()))
        {
            seed.Datasets.Add(new Dataset { OrganizationId = Guid.NewGuid(), Name = "x" });
            await seed.SaveChangesAsync();
        }

        // An unauthenticated / tenant-less context sees nothing.
        await using var ctx = NewContext(dbName, new TenantContext());
        Assert.Empty(await ctx.Datasets.ToListAsync());
    }
}
