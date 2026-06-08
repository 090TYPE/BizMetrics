namespace BizMetrics.Domain.Entities;

public enum DatasetStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

/// <summary>
/// A tenant-scoped uploaded data source (a CSV of e.g. sales). The raw file lives
/// in object storage under <see cref="StorageKey"/>; a background worker parses it
/// into <see cref="DataRow"/> records and fills in the schema and status.
/// </summary>
public class Dataset : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "csv";
    public DatasetStatus Status { get; set; } = DatasetStatus.Pending;
    public long RowCount { get; set; }

    /// <summary>Object-storage key for the raw uploaded file.</summary>
    public string? StorageKey { get; set; }

    /// <summary>Detected column headers, in order.</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>Failure reason when <see cref="Status"/> is Failed.</summary>
    public string? ErrorMessage { get; set; }

    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
