using BizMetrics.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BizMetrics.Api.Auth;

/// <summary>
/// Requires the caller's org role to be at least <see cref="Minimum"/>. Roles are
/// ordered by privilege via the enum value (Owner = 0 is highest), so "at least
/// Admin" is satisfied by Owner and Admin.
/// </summary>
public class MinimumRoleRequirement : IAuthorizationRequirement
{
    public OrgRole Minimum { get; }
    public MinimumRoleRequirement(OrgRole minimum) => Minimum = minimum;
}

public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        var roleClaim = context.User.FindFirst("org_role")?.Value;
        if (Enum.TryParse<OrgRole>(roleClaim, out var role) && role <= requirement.Minimum)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
