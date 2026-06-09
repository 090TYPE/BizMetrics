using BizMetrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Infrastructure.Analytics;

public record AnalyticsReport(AnalyticsResult Result, IReadOnlyList<string> Insights);

/// <summary>
/// Loads a dataset's rows (tenant-scoped by the DbContext filter) and runs the
/// analytics + insights engines over them.
/// </summary>
public class AnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task<AnalyticsReport> RunAsync(Guid datasetId, AnalyticsQuery query, CancellationToken ct = default)
    {
        var rows = await _db.DataRows
            .Where(r => r.DatasetId == datasetId)
            .OrderBy(r => r.RowIndex)
            .Select(r => r.Data)
            .ToListAsync(ct);

        var result = AnalyticsEngine.Run(rows, query);
        var insights = InsightsEngine.Generate(result, query);
        return new AnalyticsReport(result, insights);
    }
}
