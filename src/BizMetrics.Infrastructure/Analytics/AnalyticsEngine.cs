using System.Globalization;

namespace BizMetrics.Infrastructure.Analytics;

public record AnalyticsPoint(string Key, double Value, long Count);

public record AnalyticsResult(string Label, IReadOnlyList<AnalyticsPoint> Points);

/// <summary>
/// Computes aggregations over schema-less rows (column→value maps). Pure and
/// side-effect free so it's fully unit-testable; values are parsed with the
/// invariant culture. For this project's data sizes it runs in memory.
/// </summary>
public static class AnalyticsEngine
{
    private sealed class Accumulator
    {
        public long Count;          // all rows in the group
        public long NumericCount;   // rows with a parseable measure
        public double Sum;
        public double Min = double.PositiveInfinity;
        public double Max = double.NegativeInfinity;
    }

    public static AnalyticsResult Run(IEnumerable<Dictionary<string, string?>> rows, AnalyticsQuery q)
    {
        var groups = new Dictionary<string, Accumulator>();
        var order = new List<string>();

        foreach (var row in rows)
        {
            if (!TryKey(row, q, out var key)) continue;

            if (!groups.TryGetValue(key, out var acc))
            {
                acc = new Accumulator();
                groups[key] = acc;
                order.Add(key);
            }

            acc.Count++;

            if (q.Agg != Aggregation.Count &&
                q.Measure is not null &&
                TryNumber(row.GetValueOrDefault(q.Measure), out var n))
            {
                acc.NumericCount++;
                acc.Sum += n;
                acc.Min = Math.Min(acc.Min, n);
                acc.Max = Math.Max(acc.Max, n);
            }
        }

        var points = order
            .Select(k => new AnalyticsPoint(k, Math.Round(Value(q.Agg, groups[k]), 4), groups[k].Count))
            .ToList();

        points = Sort(points, q);
        if (q.TopN is { } top && q.Bucket == TimeBucket.None)
            points = points.Take(top).ToList();

        return new AnalyticsResult(Label(q), points);
    }

    private static double Value(Aggregation agg, Accumulator a) => agg switch
    {
        Aggregation.Count => a.Count,
        Aggregation.Sum => a.Sum,
        Aggregation.Average => a.NumericCount > 0 ? a.Sum / a.NumericCount : 0,
        Aggregation.Min => a.NumericCount > 0 ? a.Min : 0,
        Aggregation.Max => a.NumericCount > 0 ? a.Max : 0,
        _ => 0
    };

    private static bool TryKey(Dictionary<string, string?> row, AnalyticsQuery q, out string key)
    {
        key = "All";
        if (q.GroupBy is null) return true;

        var raw = row.GetValueOrDefault(q.GroupBy);

        if (q.Bucket != TimeBucket.None)
        {
            if (!TryDate(raw, out var date)) return false; // skip unparseable dates
            key = BucketKey(date, q.Bucket);
            return true;
        }

        key = string.IsNullOrWhiteSpace(raw) ? "(none)" : raw!;
        return true;
    }

    private static List<AnalyticsPoint> Sort(List<AnalyticsPoint> points, AnalyticsQuery q) =>
        q.Bucket != TimeBucket.None
            ? points.OrderBy(p => p.Key, StringComparer.Ordinal).ToList() // ISO date keys sort chronologically
            : points.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();

    private static string BucketKey(DateTime d, TimeBucket bucket) => bucket switch
    {
        TimeBucket.Day => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeBucket.Month => d.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        TimeBucket.Week => StartOfIsoWeek(d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    private static DateTime StartOfIsoWeek(DateTime d)
    {
        var diff = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
        return d.Date.AddDays(-diff);
    }

    private static bool TryNumber(string? s, out double value) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryDate(string? s, out DateTime value) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static string Label(AnalyticsQuery q)
    {
        var measure = q.Agg == Aggregation.Count ? "Count" : $"{q.Agg} of {q.Measure}";
        if (q.GroupBy is null) return measure;
        return q.Bucket != TimeBucket.None
            ? $"{measure} by {q.GroupBy} ({q.Bucket.ToString().ToLowerInvariant()})"
            : $"{measure} by {q.GroupBy}";
    }
}
