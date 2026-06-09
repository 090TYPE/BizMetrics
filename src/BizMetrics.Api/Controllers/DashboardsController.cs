using System.Text.Json;
using System.Text.Json.Serialization;
using BizMetrics.Api.Auth;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Analytics;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboards")]
public class DashboardsController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly string[] ChartTypes = ["bar", "line", "pie"];

    private readonly AppDbContext _db;
    private readonly AnalyticsService _analytics;

    public DashboardsController(AppDbContext db, AnalyticsService analytics)
    {
        _db = db;
        _analytics = analytics;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DashboardSummaryDto>>> List()
    {
        var items = await _db.Dashboards
            .OrderBy(d => d.Name)
            .Select(d => new DashboardSummaryDto(d.Id, d.Name, d.Widgets.Count, d.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<DashboardSummaryDto>> Create(CreateDashboardRequest req)
    {
        var orgId = User.GetOrganizationId();
        if (orgId is null) return Forbid();

        var dashboard = new Dashboard
        {
            OrganizationId = orgId.Value,
            Name = req.Name.Trim(),
            CreatedByUserId = User.GetUserId()
        };
        _db.Dashboards.Add(dashboard);
        await _db.SaveChangesAsync();

        return new DashboardSummaryDto(dashboard.Id, dashboard.Name, 0, dashboard.CreatedAt);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard is null) return NotFound();
        _db.Dashboards.Remove(dashboard); // widgets cascade
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Dashboard with each widget's data computed fresh from current rows.</summary>
    [HttpGet("{id:guid}/data")]
    public async Task<ActionResult<DashboardDataDto>> Data(Guid id)
    {
        var dashboard = await _db.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard is null) return NotFound();

        var widgets = new List<WidgetDataDto>();
        foreach (var w in dashboard.Widgets.OrderBy(w => w.Position))
        {
            var query = Deserialize(w.QueryJson);
            var report = await _analytics.RunAsync(w.DatasetId, query);
            widgets.Add(new WidgetDataDto(
                w.Id, w.DatasetId, w.Title, w.ChartType, w.Position,
                report.Result.Label, report.Result.Points, report.Insights));
        }

        return new DashboardDataDto(dashboard.Id, dashboard.Name, widgets);
    }

    [HttpPost("{id:guid}/widgets")]
    public async Task<ActionResult<WidgetDto>> AddWidget(Guid id, AddWidgetRequest req)
    {
        var orgId = User.GetOrganizationId();
        if (orgId is null) return Forbid();

        var dashboard = await _db.Dashboards.Include(d => d.Widgets).FirstOrDefaultAsync(d => d.Id == id);
        if (dashboard is null) return NotFound();

        // The dataset must belong to the same tenant (query filter enforces this).
        if (!await _db.Datasets.AnyAsync(d => d.Id == req.DatasetId))
            return BadRequest(new { error = "Unknown dataset." });

        var chartType = ChartTypes.Contains(req.ChartType) ? req.ChartType : "bar";
        var widget = new Widget
        {
            OrganizationId = orgId.Value,
            DashboardId = id,
            DatasetId = req.DatasetId,
            Title = req.Title.Trim(),
            ChartType = chartType,
            QueryJson = JsonSerializer.Serialize(req.Query, Json),
            Position = dashboard.Widgets.Count == 0 ? 0 : dashboard.Widgets.Max(w => w.Position) + 1
        };
        _db.Widgets.Add(widget);
        await _db.SaveChangesAsync();

        return new WidgetDto(widget.Id, widget.DatasetId, widget.Title, widget.ChartType, req.Query, widget.Position);
    }

    [HttpDelete("{id:guid}/widgets/{widgetId:guid}")]
    public async Task<IActionResult> RemoveWidget(Guid id, Guid widgetId)
    {
        var widget = await _db.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId && w.DashboardId == id);
        if (widget is null) return NotFound();
        _db.Widgets.Remove(widget);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static AnalyticsQuery Deserialize(string json) =>
        JsonSerializer.Deserialize<AnalyticsQuery>(json, Json) ?? new AnalyticsQuery();
}
