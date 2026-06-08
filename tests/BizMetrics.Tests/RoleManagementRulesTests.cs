using BizMetrics.Api.Authorization;
using BizMetrics.Domain.Entities;
using Xunit;

namespace BizMetrics.Tests;

public class RoleManagementRulesTests
{
    // --- CanChangeRole ---

    [Fact]
    public void Owner_can_promote_member_to_admin()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Owner, OrgRole.Member, OrgRole.Admin, actorIsTarget: false, ownerCount: 1);
        Assert.True(r.Allowed);
    }

    [Fact]
    public void Admin_cannot_grant_owner()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Admin, OrgRole.Member, OrgRole.Owner, actorIsTarget: false, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Admin_cannot_modify_another_admin()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Admin, OrgRole.Admin, OrgRole.Member, actorIsTarget: false, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Admin_cannot_grant_role_equal_to_own()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Admin, OrgRole.Viewer, OrgRole.Admin, actorIsTarget: false, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Nobody_can_change_their_own_role()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Owner, OrgRole.Owner, OrgRole.Admin, actorIsTarget: true, ownerCount: 2);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Cannot_demote_the_last_owner()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Owner, OrgRole.Owner, OrgRole.Admin, actorIsTarget: false, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Owner_can_demote_an_owner_when_another_owner_remains()
    {
        var r = RoleManagementRules.CanChangeRole(
            OrgRole.Owner, OrgRole.Owner, OrgRole.Admin, actorIsTarget: false, ownerCount: 2);
        Assert.True(r.Allowed);
    }

    // --- CanRemove ---

    [Fact]
    public void Admin_can_remove_a_member()
    {
        var r = RoleManagementRules.CanRemove(
            OrgRole.Admin, OrgRole.Member, actorIsTarget: false, ownerCount: 1);
        Assert.True(r.Allowed);
    }

    [Fact]
    public void Cannot_remove_yourself()
    {
        var r = RoleManagementRules.CanRemove(
            OrgRole.Admin, OrgRole.Admin, actorIsTarget: true, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Cannot_remove_the_last_owner()
    {
        var r = RoleManagementRules.CanRemove(
            OrgRole.Owner, OrgRole.Owner, actorIsTarget: false, ownerCount: 1);
        Assert.False(r.Allowed);
    }

    [Fact]
    public void Admin_cannot_remove_an_owner()
    {
        var r = RoleManagementRules.CanRemove(
            OrgRole.Admin, OrgRole.Owner, actorIsTarget: false, ownerCount: 2);
        Assert.False(r.Allowed);
    }
}
