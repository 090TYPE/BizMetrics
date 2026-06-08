namespace BizMetrics.Infrastructure.Tenancy;

/// <summary>
/// Carries the current request's tenant. Registered as scoped and populated by
/// middleware from the authenticated user's <c>org_id</c> claim. The DbContext
/// reads <see cref="OrganizationId"/> to scope every tenant query.
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid organizationId);
}

public sealed class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }
    public bool HasTenant => OrganizationId.HasValue;

    public void SetTenant(Guid organizationId) => OrganizationId = organizationId;
}
