using BizMetrics.Infrastructure.Analytics;
using Xunit;

namespace BizMetrics.Tests;

public class InsightsEngineTests
{
    [Fact]
    public void Empty_result_reports_no_data()
    {
        var insights = InsightsEngine.Generate(new AnalyticsResult("x", []), new AnalyticsQuery());
        Assert.Contains(insights, i => i.Contains("No data"));
    }

    [Fact]
    public void Categorical_reports_total_and_top_contributor_share()
    {
        var result = new AnalyticsResult("Sum of amount by product",
        [
            new AnalyticsPoint("Widget", 60, 3),
            new AnalyticsPoint("Gadget", 20, 2),
        ]);
        var insights = InsightsEngine.Generate(result,
            new AnalyticsQuery { GroupBy = "product", Measure = "amount", Agg = Aggregation.Sum });

        Assert.Contains(insights, i => i.Contains("Total") && i.Contains("80"));
        Assert.Contains(insights, i => i.Contains("Widget") && i.Contains("75")); // 60/80 = 75%
    }

    [Fact]
    public void Time_series_reports_trend_and_peak()
    {
        var result = new AnalyticsResult("Sum of amount by date (month)",
        [
            new AnalyticsPoint("2026-01", 100, 5),
            new AnalyticsPoint("2026-02", 150, 6),
        ]);
        var insights = InsightsEngine.Generate(result,
            new AnalyticsQuery { GroupBy = "date", Bucket = TimeBucket.Month, Measure = "amount", Agg = Aggregation.Sum });

        Assert.Contains(insights, i => i.Contains("trending up") && i.Contains("50")); // +50%
        Assert.Contains(insights, i => i.Contains("Peak") && i.Contains("2026-02"));
    }
}
