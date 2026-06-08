using System.Security.Claims;
using BizMetrics.Api.Auth;
using BizMetrics.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BizMetrics.Tests;

public class MinimumRoleHandlerTests
{
    private static async Task<bool> Evaluate(string? roleClaim, OrgRole minimum)
    {
        var claims = roleClaim is null ? [] : new[] { new Claim("org_role", roleClaim) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new MinimumRoleRequirement(minimum);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new MinimumRoleHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData("Owner", OrgRole.Admin, true)]   // Owner satisfies "at least Admin"
    [InlineData("Admin", OrgRole.Admin, true)]
    [InlineData("Member", OrgRole.Admin, false)] // Member does not
    [InlineData("Viewer", OrgRole.Member, false)]
    [InlineData("Member", OrgRole.Member, true)]
    public async Task Enforces_role_hierarchy(string role, OrgRole minimum, bool expected)
    {
        Assert.Equal(expected, await Evaluate(role, minimum));
    }

    [Fact]
    public async Task Missing_role_claim_fails()
    {
        Assert.False(await Evaluate(null, OrgRole.Member));
    }
}
