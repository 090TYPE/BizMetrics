using BizMetrics.Domain.Entities;

namespace BizMetrics.Api.Authorization;

public readonly record struct RuleResult(bool Allowed, string? Error)
{
    public static readonly RuleResult Ok = new(true, null);
    public static RuleResult Deny(string error) => new(false, error);
}

/// <summary>
/// Pure decision logic for member administration, kept free of HTTP/EF so it can
/// be unit-tested exhaustively. Roles are ordered by privilege via the enum value
/// (Owner = 0 is highest).
/// </summary>
public static class RoleManagementRules
{
    /// <summary>Can <paramref name="actorRole"/> change the target's role to <paramref name="newRole"/>?</summary>
    public static RuleResult CanChangeRole(
        OrgRole actorRole,
        OrgRole targetRole,
        OrgRole newRole,
        bool actorIsTarget,
        int ownerCount)
    {
        if (actorIsTarget)
            return RuleResult.Deny("You cannot change your own role.");

        // Granting or revoking ownership is reserved to Owners.
        if ((newRole == OrgRole.Owner || targetRole == OrgRole.Owner) && actorRole != OrgRole.Owner)
            return RuleResult.Deny("Only an Owner can assign or revoke the Owner role.");

        // A non-Owner may not modify someone at or above their own privilege.
        if (actorRole != OrgRole.Owner && targetRole <= actorRole)
            return RuleResult.Deny("You cannot modify a member with an equal or higher role.");

        // A non-Owner may not grant a role at or above their own privilege.
        if (actorRole != OrgRole.Owner && newRole <= actorRole)
            return RuleResult.Deny("You cannot grant a role equal to or higher than your own.");

        // Never leave an organization without an Owner.
        if (targetRole == OrgRole.Owner && newRole != OrgRole.Owner && ownerCount <= 1)
            return RuleResult.Deny("Cannot demote the last Owner.");

        return RuleResult.Ok;
    }

    /// <summary>Can <paramref name="actorRole"/> invite a new member at <paramref name="invitedRole"/>?</summary>
    public static RuleResult CanInvite(OrgRole actorRole, OrgRole invitedRole)
    {
        if (invitedRole == OrgRole.Owner && actorRole != OrgRole.Owner)
            return RuleResult.Deny("Only an Owner can invite another Owner.");

        if (actorRole != OrgRole.Owner && invitedRole <= actorRole)
            return RuleResult.Deny("You cannot invite a member at a role equal to or higher than your own.");

        return RuleResult.Ok;
    }

    /// <summary>Can <paramref name="actorRole"/> remove the target member?</summary>
    public static RuleResult CanRemove(
        OrgRole actorRole,
        OrgRole targetRole,
        bool actorIsTarget,
        int ownerCount)
    {
        if (actorIsTarget)
            return RuleResult.Deny("You cannot remove yourself.");

        if (actorRole != OrgRole.Owner && targetRole <= actorRole)
            return RuleResult.Deny("You cannot remove a member with an equal or higher role.");

        if (targetRole == OrgRole.Owner && ownerCount <= 1)
            return RuleResult.Deny("Cannot remove the last Owner.");

        return RuleResult.Ok;
    }
}
