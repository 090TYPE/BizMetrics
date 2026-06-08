using BizMetrics.Api.Auth;
using BizMetrics.Api.Authorization;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orgs")]
public class OrganizationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SessionService _sessions;

    public OrganizationsController(AppDbContext db, SessionService sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    // --- Current organization ---

    [HttpGet("current")]
    [Authorize(Policy = Policies.RequireMember)]
    public async Task<ActionResult<OrganizationDto>> Current()
    {
        var orgId = User.GetOrganizationId();
        var org = await _db.Organizations.Include(o => o.Plan).FirstOrDefaultAsync(o => o.Id == orgId);
        if (org is null) return NotFound();

        return new OrganizationDto(
            org.Id, org.Name, org.Slug,
            org.SubscriptionStatus.ToString(), org.TrialEndsAt, org.Plan?.Name);
    }

    [HttpPatch("current")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<ActionResult<OrganizationDto>> Update(UpdateOrganizationRequest req)
    {
        var orgId = User.GetOrganizationId();
        var org = await _db.Organizations.Include(o => o.Plan).FirstOrDefaultAsync(o => o.Id == orgId);
        if (org is null) return NotFound();

        org.Name = req.Name.Trim();
        await _db.SaveChangesAsync();

        return new OrganizationDto(
            org.Id, org.Name, org.Slug,
            org.SubscriptionStatus.ToString(), org.TrialEndsAt, org.Plan?.Name);
    }

    // --- Members ---

    [HttpGet("current/members")]
    [Authorize(Policy = Policies.RequireMember)]
    public async Task<ActionResult<IEnumerable<MemberDto>>> Members()
    {
        var orgId = User.GetOrganizationId();
        var members = await _db.Memberships
            .Where(m => m.OrganizationId == orgId)
            .Include(m => m.User)
            .OrderBy(m => m.Role).ThenBy(m => m.CreatedAt)
            .Select(m => new MemberDto(
                m.UserId, m.User.Email, m.User.FullName,
                m.Role.ToString(), m.Status.ToString(), m.CreatedAt))
            .ToListAsync();
        return Ok(members);
    }

    [HttpPatch("current/members/{userId:guid}/role")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<IActionResult> ChangeRole(Guid userId, ChangeRoleRequest req)
    {
        var orgId = User.GetOrganizationId();
        var actorRole = User.GetOrgRole()!.Value;

        var target = await _db.Memberships
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId);
        if (target is null) return NotFound(new { error = "Member not found." });

        var ownerCount = await _db.Memberships
            .CountAsync(m => m.OrganizationId == orgId && m.Role == OrgRole.Owner && m.Status == MembershipStatus.Active);

        var rule = RoleManagementRules.CanChangeRole(
            actorRole, target.Role, req.Role,
            actorIsTarget: userId == User.GetUserId(),
            ownerCount: ownerCount);
        if (!rule.Allowed) return Denied(rule.Error!);

        target.Role = req.Role;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("current/members/{userId:guid}")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<IActionResult> RemoveMember(Guid userId)
    {
        var orgId = User.GetOrganizationId();
        var actorRole = User.GetOrgRole()!.Value;

        var target = await _db.Memberships
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId);
        if (target is null) return NotFound(new { error = "Member not found." });

        var ownerCount = await _db.Memberships
            .CountAsync(m => m.OrganizationId == orgId && m.Role == OrgRole.Owner && m.Status == MembershipStatus.Active);

        var rule = RoleManagementRules.CanRemove(
            actorRole, target.Role,
            actorIsTarget: userId == User.GetUserId(),
            ownerCount: ownerCount);
        if (!rule.Allowed) return Denied(rule.Error!);

        _db.Memberships.Remove(target);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Org switcher ---

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<MyOrganizationDto>>> Mine()
    {
        var userId = User.GetUserId();
        var orgs = await _db.Memberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Include(m => m.Organization)
            .OrderBy(m => m.Organization.Name)
            .Select(m => new MyOrganizationDto(
                m.OrganizationId, m.Organization.Name, m.Organization.Slug, m.Role.ToString()))
            .ToListAsync();
        return Ok(orgs);
    }

    /// <summary>Re-issues a token pair scoped to another org the user belongs to.</summary>
    [HttpPost("{orgId:guid}/switch")]
    public async Task<ActionResult<AuthResponse>> Switch(Guid orgId)
    {
        var userId = User.GetUserId();
        var membership = await _db.Memberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m =>
                m.UserId == userId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active);

        if (membership is null)
            return Denied("You are not a member of that organization.");

        return await _sessions.IssueAsync(membership.User, orgId, membership.Role);
    }

    private ObjectResult Denied(string error) =>
        StatusCode(StatusCodes.Status403Forbidden, new { error });
}
