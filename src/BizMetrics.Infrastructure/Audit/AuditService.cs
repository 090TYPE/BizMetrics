using System.Text.Json;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;

namespace BizMetrics.Infrastructure.Audit;

/// <summary>
/// Records audit entries for significant actions.
/// Each call saves immediately in a silent try/catch so audit failures
/// can never break the primary request flow.
/// </summary>
public class AuditService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Records an audit entry.
    /// </summary>
    /// <param name="userId">The acting user (null for system events).</param>
    /// <param name="action">A dot-namespaced key from <see cref="BizMetrics.Domain.Audit.AuditActions"/>.</param>
    /// <param name="entityType">The entity class name, e.g. "Dataset".</param>
    /// <param name="entityId">Optional entity primary key as a string.</param>
    /// <param name="details">Optional payload; serialized to JSON.</param>
    /// <param name="orgIdOverride">Override the tenant-context org id (for webhook handlers).</param>
    /// <param name="ipAddress">Caller's IP address (extracted by the controller layer).</param>
    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        Guid? orgIdOverride = null,
        string? ipAddress = null)
    {
        try
        {
            var orgId = orgIdOverride ?? _tenant.OrganizationId;
            if (orgId is null) return;

            var detailsJson = details is null
                ? null
                : JsonSerializer.Serialize(details, new JsonSerializerOptions
                    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _db.AuditEntries.Add(new AuditEntry
            {
                OrganizationId = orgId.Value,
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = detailsJson,
                IpAddress = ipAddress
            });

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Audit must never break the primary flow.
        }
    }
}
