namespace BizMetrics.Domain.Entities;

/// <summary>
/// One parsed CSV row, stored generically as a column→value map (persisted as
/// JSONB). Keeping rows schema-less lets any shape of CSV be ingested and later
/// aggregated in Phase 4 without per-dataset tables.
/// </summary>
public class DataRow : ITenantEntity
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }

    public Guid DatasetId { get; set; }
    public Dataset Dataset { get; set; } = null!;

    public int RowIndex { get; set; }
    public Dictionary<string, string?> Data { get; set; } = new();
}
