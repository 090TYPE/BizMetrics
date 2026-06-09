namespace BizMetrics.Infrastructure.Analytics;

public enum Aggregation
{
    Count,
    Sum,
    Average,
    Min,
    Max
}

public enum TimeBucket
{
    None,
    Day,
    Week,
    Month
}

/// <summary>
/// Declarative analytics request over a dataset's rows. When <see cref="Bucket"/>
/// is set, <see cref="GroupBy"/> is treated as a date column and grouped into time
/// buckets; otherwise <see cref="GroupBy"/> is a categorical dimension.
/// </summary>
public class AnalyticsQuery
{
    /// <summary>Column to group by (a category, or a date when <see cref="Bucket"/> is set).</summary>
    public string? GroupBy { get; set; }

    public TimeBucket Bucket { get; set; } = TimeBucket.None;

    /// <summary>Numeric column to aggregate. Ignored when <see cref="Agg"/> is Count.</summary>
    public string? Measure { get; set; }

    public Aggregation Agg { get; set; } = Aggregation.Count;

    /// <summary>Keep only the top N groups (categorical only).</summary>
    public int? TopN { get; set; }
}
