using System.Globalization;

namespace BizMetrics.Infrastructure.Analytics;

/// <summary>
/// Derives short, deterministic narrative insights from an analytics result
/// (totals, top contributor and share, trend direction, peak). Pure/testable.
/// </summary>
public static class InsightsEngine
{
    public static IReadOnlyList<string> Generate(AnalyticsResult result, AnalyticsQuery q)
    {
        var insights = new List<string>();
        var points = result.Points;
        if (points.Count == 0)
        {
            insights.Add("No data matched this query.");
            return insights;
        }

        var isTimeSeries = q.Bucket != TimeBucket.None;
        var measureName = q.Agg == Aggregation.Count ? "count" : $"{q.Agg.ToString().ToLowerInvariant()} of {q.Measure}";

        // Total / span
        if (q.Agg is Aggregation.Count or Aggregation.Sum)
        {
            var total = points.Sum(p => p.Value);
            insights.Add($"Total {measureName} across {points.Count} {(isTimeSeries ? "periods" : "groups")}: {Fmt(total)}.");
        }

        if (isTimeSeries)
        {
            // Trend: compare first vs last point.
            var first = points[0];
            var last = points[^1];
            if (points.Count >= 2 && first.Value != 0)
            {
                var change = (last.Value - first.Value) / Math.Abs(first.Value) * 100;
                var dir = change > 0 ? "up" : change < 0 ? "down" : "flat";
                insights.Add($"{Capitalize(measureName)} is trending {dir} {Fmt(Math.Abs(change))}% from {first.Key} to {last.Key}.");
            }

            var peak = points.MaxBy(p => p.Value)!;
            insights.Add($"Peak was {Fmt(peak.Value)} on {peak.Key}.");
        }
        else
        {
            // Top contributor and its share.
            var top = points[0]; // already sorted desc by value
            var total = points.Sum(p => p.Value);
            if (total > 0)
            {
                var share = top.Value / total * 100;
                insights.Add($"{top.Key} leads with {Fmt(top.Value)} ({Fmt(share)}% of the total).");
            }
            else
            {
                insights.Add($"{top.Key} leads with {Fmt(top.Value)}.");
            }

            if (points.Count > 1)
            {
                var bottom = points[^1];
                insights.Add($"{bottom.Key} is the smallest at {Fmt(bottom.Value)}.");
            }
        }

        return insights;
    }

    private static string Fmt(double v) =>
        (Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString("N0", CultureInfo.InvariantCulture)
                                            : Math.Round(v, 2).ToString("N2", CultureInfo.InvariantCulture));

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
