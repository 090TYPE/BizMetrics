namespace BizMetrics.Domain.Entities;

public enum DatasetStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

/// <summary>
/// A tenant-scoped uploaded data source (e.g. a CSV of sales). Included in
/// Phase 0 as the first real tenant entity so the global query filter and the
/// cross-tenant isolation tests have something concrete to operate on.
/// </summary>
public class Dataset : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "csv";
    public DatasetStatus Status { get; set; } = DatasetStatus.Pending;
    public long RowCount { get; set; }

    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
