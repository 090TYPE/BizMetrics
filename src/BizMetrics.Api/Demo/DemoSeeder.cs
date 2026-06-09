using System.Text.Json;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Demo;

/// <summary>
/// Seeds a realistic demo workspace that showcases all product features.
/// Idempotent: skips quietly if the demo org already exists.
///
/// Demo credentials: demo@bizmetrics.io / demo1234
/// </summary>
public static class DemoSeeder
{
    public const string DemoEmail = "demo@bizmetrics.io";
    public const string DemoPassword = "demo1234";
    private const string DemoOrgSlug = "demo-acme";

    public static async Task SeedAsync(AppDbContext db)
    {
        // Idempotency guard
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == DemoOrgSlug))
            return;

        // ── 1. User ─────────────────────────────────────────────────────────
        var user = new User
        {
            Email = DemoEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
            FullName = "Alex Demo",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        // ── 2. Organization ──────────────────────────────────────────────────
        var proPlan = await db.Plans.FirstOrDefaultAsync(p => p.Name == "Pro");
        var org = new Organization
        {
            Name = "Acme Corp",
            Slug = DemoOrgSlug,
            PlanId = proPlan?.Id,
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow.AddDays(-30) // joined a month ago
        };
        db.Organizations.Add(org);

        // ── 3. Membership (Owner) ────────────────────────────────────────────
        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = OrgRole.Owner,
            Status = MembershipStatus.Active
        };
        db.Memberships.Add(membership);

        // ── 4. Sales dataset ─────────────────────────────────────────────────
        var salesColumns = new List<string>
            { "Month", "Product", "Category", "Revenue", "Units", "Region" };
        var salesDs = new Dataset
        {
            OrganizationId = org.Id,
            Name = "Sales 2024",
            Status = DatasetStatus.Ready,
            Columns = salesColumns,
            RowCount = SalesRows.Length,
            UploadedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-28),
            ProcessedAt = DateTime.UtcNow.AddDays(-28).AddSeconds(3)
        };
        db.Datasets.Add(salesDs);

        // ── 5. Marketing dataset ─────────────────────────────────────────────
        var mktColumns = new List<string>
            { "Campaign", "Channel", "Spend", "Conversions", "CPC", "Month" };
        var mktDs = new Dataset
        {
            OrganizationId = org.Id,
            Name = "Marketing Q1-Q2 2024",
            Status = DatasetStatus.Ready,
            Columns = mktColumns,
            RowCount = MarketingRows.Length,
            UploadedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            ProcessedAt = DateTime.UtcNow.AddDays(-20).AddSeconds(2)
        };
        db.Datasets.Add(mktDs);

        await db.SaveChangesAsync(); // get IDs before inserting rows

        // ── 6. Rows ──────────────────────────────────────────────────────────
        var salesDataRows = SalesRows.Select((r, i) => new DataRow
        {
            OrganizationId = org.Id,
            DatasetId = salesDs.Id,
            RowIndex = i,
            Data = r
        });
        db.DataRows.AddRange(salesDataRows);

        var mktDataRows = MarketingRows.Select((r, i) => new DataRow
        {
            OrganizationId = org.Id,
            DatasetId = mktDs.Id,
            RowIndex = i,
            Data = r
        });
        db.DataRows.AddRange(mktDataRows);

        // ── 7. Dashboard ─────────────────────────────────────────────────────
        var dashboard = new Dashboard
        {
            OrganizationId = org.Id,
            Name = "Sales Overview",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-25)
        };
        db.Dashboards.Add(dashboard);

        await db.SaveChangesAsync(); // get dashboard ID

        var widgets = new List<Widget>
        {
            new Widget
            {
                OrganizationId = org.Id,
                DashboardId = dashboard.Id,
                DatasetId = salesDs.Id,
                Title = "Revenue by Category",
                ChartType = "pie",
                Position = 0,
                QueryJson = JsonSerializer.Serialize(new
                {
                    groupBy = "Category", bucket = "None", measure = "Revenue",
                    agg = "Sum", topN = (int?)null
                })
            },
            new Widget
            {
                OrganizationId = org.Id,
                DashboardId = dashboard.Id,
                DatasetId = salesDs.Id,
                Title = "Monthly Revenue Trend",
                ChartType = "line",
                Position = 1,
                QueryJson = JsonSerializer.Serialize(new
                {
                    groupBy = "Month", bucket = "None", measure = "Revenue",
                    agg = "Sum", topN = (int?)null
                })
            },
            new Widget
            {
                OrganizationId = org.Id,
                DashboardId = dashboard.Id,
                DatasetId = salesDs.Id,
                Title = "Top Products by Units Sold",
                ChartType = "bar",
                Position = 2,
                QueryJson = JsonSerializer.Serialize(new
                {
                    groupBy = "Product", bucket = "None", measure = "Units",
                    agg = "Sum", topN = (int?)5
                })
            }
        };
        db.Widgets.AddRange(widgets);

        await db.SaveChangesAsync();
    }

    // ── Seed data ─────────────────────────────────────────────────────────────
    // 48 rows: 12 months × 4 products

    private static readonly Dictionary<string, string?>[] SalesRows =
    [
        R("2024-01","Starter Pack","Software","4200","210","North"),
        R("2024-01","Pro Suite","Software","9800","98","North"),
        R("2024-01","Analytics Add-on","Software","3100","155","South"),
        R("2024-01","Consulting","Services","6500","13","West"),
        R("2024-02","Starter Pack","Software","4600","230","North"),
        R("2024-02","Pro Suite","Software","10200","102","South"),
        R("2024-02","Analytics Add-on","Software","3400","170","North"),
        R("2024-02","Consulting","Services","7200","14","East"),
        R("2024-03","Starter Pack","Software","5100","255","East"),
        R("2024-03","Pro Suite","Software","11500","115","North"),
        R("2024-03","Analytics Add-on","Software","3900","195","West"),
        R("2024-03","Consulting","Services","8100","16","North"),
        R("2024-04","Starter Pack","Software","5400","270","West"),
        R("2024-04","Pro Suite","Software","12100","121","East"),
        R("2024-04","Analytics Add-on","Software","4200","210","North"),
        R("2024-04","Consulting","Services","7800","15","South"),
        R("2024-05","Starter Pack","Software","5900","295","North"),
        R("2024-05","Pro Suite","Software","13400","134","West"),
        R("2024-05","Analytics Add-on","Software","4700","235","East"),
        R("2024-05","Consulting","Services","9200","18","North"),
        R("2024-06","Starter Pack","Software","6200","310","South"),
        R("2024-06","Pro Suite","Software","14100","141","North"),
        R("2024-06","Analytics Add-on","Software","5100","255","West"),
        R("2024-06","Consulting","Services","9800","19","East"),
        R("2024-07","Starter Pack","Software","5800","290","East"),
        R("2024-07","Pro Suite","Software","13200","132","South"),
        R("2024-07","Analytics Add-on","Software","4800","240","North"),
        R("2024-07","Consulting","Services","8700","17","West"),
        R("2024-08","Starter Pack","Software","6400","320","North"),
        R("2024-08","Pro Suite","Software","14800","148","East"),
        R("2024-08","Analytics Add-on","Software","5500","275","South"),
        R("2024-08","Consulting","Services","10200","20","North"),
        R("2024-09","Starter Pack","Software","7100","355","West"),
        R("2024-09","Pro Suite","Software","16200","162","North"),
        R("2024-09","Analytics Add-on","Software","6100","305","East"),
        R("2024-09","Consulting","Services","11400","22","South"),
        R("2024-10","Starter Pack","Software","7800","390","North"),
        R("2024-10","Pro Suite","Software","17500","175","West"),
        R("2024-10","Analytics Add-on","Software","6800","340","North"),
        R("2024-10","Consulting","Services","12200","24","East"),
        R("2024-11","Starter Pack","Software","9100","455","East"),
        R("2024-11","Pro Suite","Software","20300","203","North"),
        R("2024-11","Analytics Add-on","Software","7900","395","South"),
        R("2024-11","Consulting","Services","14500","29","West"),
        R("2024-12","Starter Pack","Software","10400","520","North"),
        R("2024-12","Pro Suite","Software","23100","231","East"),
        R("2024-12","Analytics Add-on","Software","9200","460","North"),
        R("2024-12","Consulting","Services","16800","33","South"),
    ];

    private static readonly Dictionary<string, string?>[] MarketingRows =
    [
        R6("Spring Sale","Email","1200","340","3.53","2024-01"),
        R6("Brand Awareness","Social","3400","890","3.82","2024-01"),
        R6("Product Launch","Search","5600","1240","4.52","2024-01"),
        R6("Spring Sale","Email","1100","310","3.55","2024-02"),
        R6("Brand Awareness","Social","3100","810","3.83","2024-02"),
        R6("Product Launch","Search","5200","1150","4.52","2024-02"),
        R6("Retargeting","Display","2400","620","3.87","2024-02"),
        R6("Spring Sale","Email","1400","410","3.41","2024-03"),
        R6("Brand Awareness","Social","3800","1020","3.73","2024-03"),
        R6("Product Launch","Search","6100","1380","4.42","2024-03"),
        R6("Retargeting","Display","2700","720","3.75","2024-03"),
        R6("Partner Co-op","Referral","1800","490","3.67","2024-03"),
        R6("Spring Sale","Email","1600","490","3.27","2024-04"),
        R6("Brand Awareness","Social","4200","1150","3.65","2024-04"),
        R6("Summer Promo","Search","7200","1620","4.44","2024-04"),
        R6("Retargeting","Display","3100","840","3.69","2024-04"),
        R6("Partner Co-op","Referral","2200","610","3.61","2024-04"),
        R6("Summer Promo","Email","1900","580","3.28","2024-05"),
        R6("Brand Awareness","Social","4600","1290","3.57","2024-05"),
        R6("Summer Promo","Search","8100","1840","4.40","2024-05"),
        R6("Retargeting","Display","3500","960","3.65","2024-05"),
        R6("Partner Co-op","Referral","2600","740","3.51","2024-05"),
        R6("Summer Promo","Email","2200","680","3.24","2024-06"),
        R6("Brand Awareness","Social","5100","1450","3.52","2024-06"),
        R6("Summer Promo","Search","9200","2110","4.36","2024-06"),
        R6("Retargeting","Display","3900","1080","3.61","2024-06"),
        R6("Partner Co-op","Referral","3000","870","3.45","2024-06"),
        R6("Influencer","Social","6800","1920","3.54","2024-06"),
        R6("Summer Promo","Email","1800","550","3.27","2024-07"),
        R6("Influencer","Social","7400","2110","3.51","2024-07"),
    ];

    // Helpers to create row dictionaries
    private static Dictionary<string, string?> R(
        string month, string product, string category,
        string revenue, string units, string region) =>
        new()
        {
            ["Month"] = month, ["Product"] = product, ["Category"] = category,
            ["Revenue"] = revenue, ["Units"] = units, ["Region"] = region
        };

    private static Dictionary<string, string?> R6(
        string campaign, string channel, string spend,
        string conversions, string cpc, string month) =>
        new()
        {
            ["Campaign"] = campaign, ["Channel"] = channel, ["Spend"] = spend,
            ["Conversions"] = conversions, ["CPC"] = cpc, ["Month"] = month
        };
}
