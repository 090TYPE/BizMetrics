using BizMetrics.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BizMetrics.Api.Auth;

/// <summary>Named authorization policies, each requiring a minimum org role.</summary>
public static class Policies
{
    public const string RequireOwner = "RequireOwner";
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireMember = "RequireMember";

    public static void AddOrgPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireOwner, p => p.Requirements.Add(new MinimumRoleRequirement(OrgRole.Owner)));
        options.AddPolicy(RequireAdmin, p => p.Requirements.Add(new MinimumRoleRequirement(OrgRole.Admin)));
        options.AddPolicy(RequireMember, p => p.Requirements.Add(new MinimumRoleRequirement(OrgRole.Member)));
    }
}
