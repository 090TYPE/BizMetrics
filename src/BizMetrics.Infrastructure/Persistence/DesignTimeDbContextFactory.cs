using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BizMetrics.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core CLI (`dotnet ef migrations`). Supplies a no-op
/// tenant context and a connection string so the tools can build the model
/// without the full DI container.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("BIZMETRICS_DB")
                   ?? "Host=localhost;Port=5432;Database=bizmetrics;Username=bizmetrics;Password=bizmetrics";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options, new TenantContext());
    }
}
