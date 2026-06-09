using BizMetrics.Infrastructure.Analytics;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

/// <summary>Ad-hoc analytics over a dataset (used by the Explore view before saving widgets).</summary>
[ApiController]
[Authorize]
[Route("api/datasets/{datasetId:guid}")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AnalyticsService _analytics;

    public AnalyticsController(AppDbContext db, AnalyticsService analytics)
    {
        _db = db;
        _analytics = analytics;
    }

    public record QueryResultDto(string Label, IReadOnlyList<AnalyticsPoint> Points, IReadOnlyList<string> Insights);

    [HttpPost("query")]
    public async Task<ActionResult<QueryResultDto>> Query(Guid datasetId, AnalyticsQuery query)
    {
        // Confirm the dataset exists for this tenant (filtered) before querying.
        if (!await _db.Datasets.AnyAsync(d => d.Id == datasetId))
            return NotFound();

        var report = await _analytics.RunAsync(datasetId, query);
        return new QueryResultDto(report.Result.Label, report.Result.Points, report.Insights);
    }
}
