namespace BizMetrics.Domain.Entities;

/// <summary>
/// Immutable record of an action taken by a user or the system.
/// NOT a tenant entity — filtered by OrganizationId in queries, never auto-filtered.
/// This lets background workers (webhooks) insert entries without a tenant context.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The organization this entry belongs to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The user who triggered the action (null for system/webhook events).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Dot-namespaced action key, e.g. "dataset.uploaded".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Affected entity class name, e.g. "Dataset".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>String representation of the affected entity's ID.</summary>
    public string? EntityId { get; set; }

    /// <summary>JSON payload with extra context (new name, role change, etc.).</summary>
    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
