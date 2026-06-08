using BizMetrics.Infrastructure.Tenancy;

namespace BizMetrics.Api.Auth;

/// <summary>
/// Reads the <c>org_id</c> claim from the authenticated principal and populates
/// the scoped <see cref="ITenantContext"/> before controllers (and the DbContext
/// query filter) run. No claim → no tenant → tenant-scoped queries return empty.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var orgClaim = context.User.FindFirst("org_id")?.Value;
        if (Guid.TryParse(orgClaim, out var orgId))
            tenant.SetTenant(orgId);

        await _next(context);
    }
}
