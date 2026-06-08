using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Storage;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizMetrics.Infrastructure.Processing;

/// <summary>
/// Drains the dataset queue and processes each upload: pull the raw CSV from object
/// storage, parse it, and persist the rows as JSONB — moving the dataset through
/// Processing → Ready (or Failed). Runs in its own DI scope per job so it can use
/// the scoped DbContext outside any HTTP request.
/// </summary>
public class DatasetProcessingService : BackgroundService
{
    private const int BatchSize = 1000;

    private readonly ChannelDatasetProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatasetProcessingService> _log;

    public DatasetProcessingService(
        ChannelDatasetProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DatasetProcessingService> log)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var datasetId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(datasetId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Failed to process dataset {DatasetId}", datasetId);
                await MarkFailedAsync(datasetId, ex.Message);
            }
        }
    }

    private async Task ProcessAsync(Guid datasetId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // The worker runs without a request, so bootstrap the dataset past the
        // tenant filter, then scope this unit of work to its organization.
        var dataset = await db.Datasets.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == datasetId, ct);
        if (dataset is null || dataset.StorageKey is null)
        {
            _log.LogWarning("Dataset {DatasetId} not found or has no file", datasetId);
            return;
        }
        tenant.SetTenant(dataset.OrganizationId);

        dataset.Status = DatasetStatus.Processing;
        await db.SaveChangesAsync(ct);

        await using var stream = await storage.GetAsync(dataset.StorageKey, ct);
        var parsed = CsvParser.Parse(stream);

        // Replace any prior rows (idempotent re-processing).
        await db.DataRows.Where(r => r.DatasetId == datasetId).ExecuteDeleteAsync(ct);

        var index = 0;
        var buffer = new List<DataRow>(BatchSize);
        foreach (var row in parsed.Rows)
        {
            buffer.Add(new DataRow
            {
                OrganizationId = dataset.OrganizationId,
                DatasetId = datasetId,
                RowIndex = index++,
                Data = row
            });
            if (buffer.Count >= BatchSize)
            {
                db.DataRows.AddRange(buffer);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            db.DataRows.AddRange(buffer);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        // Re-load (change tracker was cleared) and finalize.
        dataset = await db.Datasets.IgnoreQueryFilters().FirstAsync(d => d.Id == datasetId, ct);
        dataset.Columns = parsed.Columns;
        dataset.RowCount = parsed.Rows.Count;
        dataset.Status = DatasetStatus.Ready;
        dataset.ProcessedAt = DateTime.UtcNow;
        dataset.ErrorMessage = null;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Processed dataset {DatasetId}: {Rows} rows", datasetId, parsed.Rows.Count);
    }

    private async Task MarkFailedAsync(Guid datasetId, string error)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dataset = await db.Datasets.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == datasetId);
            if (dataset is null) return;
            dataset.Status = DatasetStatus.Failed;
            dataset.ErrorMessage = error.Length > 500 ? error[..500] : error;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not mark dataset {DatasetId} as failed", datasetId);
        }
    }
}
