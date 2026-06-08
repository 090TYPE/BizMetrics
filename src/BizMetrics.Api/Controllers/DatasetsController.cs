using BizMetrics.Api.Auth;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Processing;
using BizMetrics.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Tenant-scoped datasets. Uploads are stored in object storage and parsed by a
/// background worker; every query here is constrained to the caller's org by the
/// global query filter.
/// </summary>
[ApiController]
[Authorize]
[Route("api/datasets")]
public class DatasetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IDatasetProcessingQueue _queue;

    public DatasetsController(AppDbContext db, IObjectStorage storage, IDatasetProcessingQueue queue)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
    }

    public record DatasetDto(
        Guid Id, string Name, string Status, long RowCount,
        IReadOnlyList<string> Columns, string? ErrorMessage,
        DateTime CreatedAt, DateTime? ProcessedAt);

    public record RowsDto(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string?>> Rows, long Total);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DatasetDto>>> List()
    {
        var items = await _db.Datasets
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DatasetDto>> Get(Guid id)
    {
        var d = await _db.Datasets.FirstOrDefaultAsync(x => x.Id == id);
        return d is null ? NotFound() : ToDto(d);
    }

    /// <summary>Uploads a CSV, stores it, and queues it for background parsing.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<ActionResult<DatasetDto>> Upload([FromForm] IFormFile file, [FromForm] string? name)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A non-empty file is required." });

        var orgId = User.GetOrganizationId();
        if (orgId is null) return Forbid();

        var datasetId = Guid.NewGuid();
        var safeName = Path.GetFileName(file.FileName);
        var key = $"datasets/{orgId}/{datasetId}/{safeName}";

        await using (var stream = file.OpenReadStream())
            await _storage.PutAsync(key, stream, file.ContentType ?? "text/csv");

        var dataset = new Dataset
        {
            Id = datasetId,
            OrganizationId = orgId.Value,
            Name = string.IsNullOrWhiteSpace(name) ? safeName : name.Trim(),
            StorageKey = key,
            Status = DatasetStatus.Pending,
            UploadedByUserId = User.GetUserId()
        };
        _db.Datasets.Add(dataset);
        await _db.SaveChangesAsync();

        await _queue.EnqueueAsync(datasetId);

        return CreatedAtAction(nameof(Get), new { id = datasetId }, ToDto(dataset));
    }

    /// <summary>Paged preview of a processed dataset's rows.</summary>
    [HttpGet("{id:guid}/rows")]
    public async Task<ActionResult<RowsDto>> Rows(Guid id, int skip = 0, int take = 50)
    {
        take = Math.Clamp(take, 1, 500);
        var dataset = await _db.Datasets.FirstOrDefaultAsync(d => d.Id == id);
        if (dataset is null) return NotFound();

        var rows = await _db.DataRows
            .Where(r => r.DatasetId == id)
            .OrderBy(r => r.RowIndex)
            .Skip(skip).Take(take)
            .Select(r => r.Data)
            .ToListAsync();

        return new RowsDto(dataset.Columns, rows, dataset.RowCount);
    }

    [HttpGet("{id:guid}/download-url")]
    public async Task<ActionResult<object>> DownloadUrl(Guid id)
    {
        var dataset = await _db.Datasets.FirstOrDefaultAsync(d => d.Id == id);
        if (dataset?.StorageKey is null) return NotFound();

        var url = _storage.GetPresignedDownloadUrl(dataset.StorageKey, TimeSpan.FromMinutes(10));
        return Ok(new { url });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var dataset = await _db.Datasets.FirstOrDefaultAsync(d => d.Id == id);
        if (dataset is null) return NotFound();

        await _db.DataRows.Where(r => r.DatasetId == id).ExecuteDeleteAsync();
        _db.Datasets.Remove(dataset);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static DatasetDto ToDto(Dataset d) => new(
        d.Id, d.Name, d.Status.ToString(), d.RowCount,
        d.Columns, d.ErrorMessage, d.CreatedAt, d.ProcessedAt);
}
