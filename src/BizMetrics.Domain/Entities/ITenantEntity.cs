namespace BizMetrics.Domain.Entities;

/// <summary>
/// Marker for tenant-scoped entities. The DbContext applies a global query
/// filter on <see cref="OrganizationId"/> for every type implementing this,
/// so cross-tenant reads are impossible to write by accident.
/// </summary>
public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
}
