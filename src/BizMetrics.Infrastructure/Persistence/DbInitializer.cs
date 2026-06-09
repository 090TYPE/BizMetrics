using BizMetrics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Infrastructure.Persistence;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the static billing plans.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="proPriceId">Optional Stripe Price ID for the Pro plan.</param>
    /// <param name="businessPriceId">Optional Stripe Price ID for the Business plan.</param>
    public static async Task MigrateAndSeedAsync(
        AppDbContext db,
        string? proPriceId = null,
        string? businessPriceId = null)
    {
        await db.Database.MigrateAsync();

        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Name = "Free",     MaxUsers = 2,  MaxDatasets = 3,   MaxRows = 10_000,     PriceMonthly = 0 },
                new Plan { Name = "Pro",      MaxUsers = 10, MaxDatasets = 50,  MaxRows = 1_000_000,  PriceMonthly = 29,
                           StripePriceId = string.IsNullOrWhiteSpace(proPriceId) ? null : proPriceId },
                new Plan { Name = "Business", MaxUsers = 50, MaxDatasets = 500, MaxRows = 25_000_000, PriceMonthly = 99,
                           StripePriceId = string.IsNullOrWhiteSpace(businessPriceId) ? null : businessPriceId });
            await db.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(proPriceId) || !string.IsNullOrWhiteSpace(businessPriceId))
        {
            // Backfill price IDs in case the plans were seeded before Stripe was configured.
            var plans = await db.Plans.ToListAsync();
            foreach (var p in plans)
            {
                if (p.Name == "Pro" && !string.IsNullOrWhiteSpace(proPriceId) && p.StripePriceId != proPriceId)
                    p.StripePriceId = proPriceId;
                if (p.Name == "Business" && !string.IsNullOrWhiteSpace(businessPriceId) && p.StripePriceId != businessPriceId)
                    p.StripePriceId = businessPriceId;
            }
            await db.SaveChangesAsync();
        }
    }
}
