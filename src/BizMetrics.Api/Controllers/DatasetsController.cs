using System.Security.Claims;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Minimal tenant-scoped resource demonstrating the global query filter end to
/// end. Every query here is automatically constrained to the caller's org — the
/// controller never writes a WHERE OrganizationId clause itself. Real CSV upload
/// and processing land in Phase 3.
/// </summary>
[ApiController]
[Authorize]
[Route("api/datasets")]
public class DatasetsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DatasetsController(AppDbContext db) => _db = db;

    public record CreateDatasetRequest(string Name);
    public record DatasetDto(Guid Id, string Name, string Status, long RowCount, DateTime CreatedAt);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DatasetDto>>> List()
    {
        var items = await _db.Datasets
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DatasetDto(d.Id, d.Name, d.Status.ToString(), d.RowCount, d.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<DatasetDto>> Create(CreateDatasetRequest req)
    {
        var orgId = GetOrgId();
        if (orgId is null) return Forbid();

        var ds = new Dataset
        {
            OrganizationId = orgId.Value,
            Name = req.Name.Trim(),
            UploadedByUserId = GetUserId(),
            Status = DatasetStatus.Pending
        };
        _db.Datasets.Add(ds);
        await _db.SaveChangesAsync();

        var dto = new DatasetDto(ds.Id, ds.Name, ds.Status.ToString(), ds.RowCount, ds.CreatedAt);
        return CreatedAtAction(nameof(List), new { id = ds.Id }, dto);
    }

    private Guid? GetOrgId() =>
        Guid.TryParse(User.FindFirst("org_id")?.Value, out var id) ? id : null;

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);
}
