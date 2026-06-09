using BizMetrics.Infrastructure.Analytics;
using Xunit;

namespace BizMetrics.Tests;

public class AnalyticsEngineTests
{
    private static List<Dictionary<string, string?>> Sales() =>
    [
        Row("2026-01-01", "Widget", "10"),
        Row("2026-01-01", "Gadget", "5"),
        Row("2026-01-08", "Widget", "20"),
        Row("2026-02-03", "Widget", "30"),
        Row("2026-02-03", "Gadget", "15"),
    ];

    private static Dictionary<string, string?> Row(string date, string product, string amount) =>
        new() { ["date"] = date, ["product"] = product, ["amount"] = amount };

    [Fact]
    public void Sum_by_category_sorts_descending_with_shares()
    {
        var r = AnalyticsEngine.Run(Sales(), new AnalyticsQuery
        {
            GroupBy = "product", Measure = "amount", Agg = Aggregation.Sum
        });

        Assert.Equal(2, r.Points.Count);
        Assert.Equal("Widget", r.Points[0].Key);   // 10+20+30 = 60
        Assert.Equal(60, r.Points[0].Value);
        Assert.Equal("Gadget", r.Points[1].Key);   // 5+15 = 20
        Assert.Equal(20, r.Points[1].Value);
    }

    [Fact]
    public void Count_ignores_measure_and_counts_rows()
    {
        var r = AnalyticsEngine.Run(Sales(), new AnalyticsQuery { GroupBy = "product", Agg = Aggregation.Count });
        Assert.Equal(3, r.Points.Single(p => p.Key == "Widget").Value);
        Assert.Equal(2, r.Points.Single(p => p.Key == "Gadget").Value);
    }

    [Fact]
    public void Average_divides_by_numeric_rows_only()
    {
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["g"] = "a", ["m"] = "10" },
            new() { ["g"] = "a", ["m"] = "20" },
            new() { ["g"] = "a", ["m"] = "n/a" }, // non-numeric, excluded from avg
        };
        var r = AnalyticsEngine.Run(rows, new AnalyticsQuery { GroupBy = "g", Measure = "m", Agg = Aggregation.Average });
        Assert.Equal(15, r.Points.Single().Value); // (10+20)/2
        Assert.Equal(3, r.Points.Single().Count);  // but all 3 rows counted in the group
    }

    [Fact]
    public void Time_series_buckets_by_month_in_chronological_order()
    {
        var r = AnalyticsEngine.Run(Sales(), new AnalyticsQuery
        {
            GroupBy = "date", Bucket = TimeBucket.Month, Measure = "amount", Agg = Aggregation.Sum
        });

        Assert.Equal(2, r.Points.Count);
        Assert.Equal("2026-01", r.Points[0].Key); // 10+5+20 = 35
        Assert.Equal(35, r.Points[0].Value);
        Assert.Equal("2026-02", r.Points[1].Key); // 30+15 = 45
        Assert.Equal(45, r.Points[1].Value);
    }

    [Fact]
    public void TopN_limits_categorical_groups()
    {
        var r = AnalyticsEngine.Run(Sales(), new AnalyticsQuery
        {
            GroupBy = "product", Measure = "amount", Agg = Aggregation.Sum, TopN = 1
        });
        Assert.Single(r.Points);
        Assert.Equal("Widget", r.Points[0].Key);
    }

    [Fact]
    public void Unparseable_dates_are_skipped_in_time_series()
    {
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["date"] = "2026-01-01", ["m"] = "1" },
            new() { ["date"] = "not-a-date", ["m"] = "9" },
        };
        var r = AnalyticsEngine.Run(rows, new AnalyticsQuery
        {
            GroupBy = "date", Bucket = TimeBucket.Day, Measure = "m", Agg = Aggregation.Sum
        });
        Assert.Single(r.Points);
        Assert.Equal(1, r.Points[0].Value);
    }
}
