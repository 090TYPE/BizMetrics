using System.Security.Claims;
using BizMetrics.Domain.Entities;

namespace BizMetrics.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }

    public static Guid? GetOrganizationId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("org_id")?.Value, out var id) ? id : null;

    public static OrgRole? GetOrgRole(this ClaimsPrincipal user) =>
        Enum.TryParse<OrgRole>(user.FindFirst("org_role")?.Value, out var role) ? role : null;
}
