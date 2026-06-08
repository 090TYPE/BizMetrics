using BizMetrics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Infrastructure.Persistence;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the static billing plans.</summary>
    public static async Task MigrateAndSeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Name = "Free",       MaxUsers = 2,   MaxDatasets = 3,   MaxRows = 10_000,     PriceMonthly = 0 },
                new Plan { Name = "Pro",        MaxUsers = 10,  MaxDatasets = 50,  MaxRows = 1_000_000,  PriceMonthly = 29 },
                new Plan { Name = "Business",   MaxUsers = 50,  MaxDatasets = 500, MaxRows = 25_000_000, PriceMonthly = 99 });
            await db.SaveChangesAsync();
        }
    }
}
