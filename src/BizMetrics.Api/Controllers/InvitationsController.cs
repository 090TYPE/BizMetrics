using BizMetrics.Api.Auth;
using BizMetrics.Api.Authorization;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Billing;
using BizMetrics.Infrastructure.Email;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

[ApiController]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailQueue _email;
    private readonly IConfiguration _config;
    private readonly PlanGuard _guard;

    public InvitationsController(
        AppDbContext db, ITokenService tokens, IEmailQueue email,
        IConfiguration config, PlanGuard guard)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _config = config;
        _guard = guard;
    }

    private const int ExpiryDays = 7;
    private string FrontendUrl => _config["App:FrontendUrl"] ?? "http://localhost:5173";

    // --- Admin: manage invitations for the current org ---

    [HttpPost("api/orgs/current/invitations")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<ActionResult<InvitationDto>> Create(CreateInvitationRequest req)
    {
        var orgId = User.GetOrganizationId()!.Value;
        var actorRole = User.GetOrgRole()!.Value;
        var email = req.Email.Trim().ToLowerInvariant();

        var rule = RoleManagementRules.CanInvite(actorRole, req.Role);
        if (!rule.Allowed) return Denied(rule.Error!);

        // Enforce member-count plan limit before creating the invitation
        var (limitOk, limitReason) = await _guard.CanInviteMemberAsync();
        if (!limitOk)
            return StatusCode(StatusCodes.Status402PaymentRequired, new { error = limitReason });

        var alreadyMember = await _db.Memberships
            .Include(m => m.User)
            .AnyAsync(m => m.OrganizationId == orgId && m.User.Email == email && m.Status == MembershipStatus.Active);
        if (alreadyMember)
            return Conflict(new { error = "That email is already a member of this organization." });

        // One live invitation per email/org: supersede any existing pending one.
        var existing = await _db.Invitations
            .Where(i => i.OrganizationId == orgId && i.Email == email && i.Status == InvitationStatus.Pending)
            .ToListAsync();
        foreach (var old in existing) old.Status = InvitationStatus.Revoked;

        var (rawToken, tokenHash) = _tokens.CreateOpaqueToken();
        var invite = new Invitation
        {
            OrganizationId = orgId,
            Email = email,
            Role = req.Role,
            TokenHash = tokenHash,
            InvitedByUserId = User.GetUserId(),
            ExpiresAt = DateTime.UtcNow.AddDays(ExpiryDays)
        };
        _db.Invitations.Add(invite);
        await _db.SaveChangesAsync();

        var org = await _db.Organizations.FirstAsync(o => o.Id == orgId);
        var link = $"{FrontendUrl}/accept?token={rawToken}";
        await _email.EnqueueAsync(new EmailMessage(
            To: email,
            Subject: $"You're invited to {org.Name} on BizMetrics",
            Body: $"You've been invited to join {org.Name} as {req.Role}. Accept here: {link}"));

        return new InvitationDto(invite.Id, invite.Email, invite.Role.ToString(),
            invite.Status.ToString(), invite.ExpiresAt, invite.CreatedAt);
    }

    [HttpGet("api/orgs/current/invitations")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<ActionResult<IEnumerable<InvitationDto>>> List()
    {
        var orgId = User.GetOrganizationId();
        var items = await _db.Invitations
            .Where(i => i.OrganizationId == orgId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role.ToString(),
                i.Status.ToString(), i.ExpiresAt, i.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpDelete("api/orgs/current/invitations/{id:guid}")]
    [Authorize(Policy = Policies.RequireAdmin)]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var orgId = User.GetOrganizationId();
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == orgId);
        if (invite is null) return NotFound();

        invite.Status = InvitationStatus.Revoked;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Invitee: preview and accept ---

    [HttpGet("api/invitations/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitationPreviewDto>> Preview(string token)
    {
        var invite = await _db.Invitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.TokenHash == _tokens.HashToken(token));
        if (invite is null) return NotFound();

        return new InvitationPreviewDto(
            invite.Organization.Name, invite.Email, invite.Role.ToString(), invite.IsRedeemable);
    }

    [HttpPost("api/invitations/accept")]
    public async Task<IActionResult> Accept(AcceptInvitationRequest req)
    {
        var invite = await _db.Invitations
            .FirstOrDefaultAsync(i => i.TokenHash == _tokens.HashToken(req.Token));
        if (invite is null || !invite.IsRedeemable)
            return BadRequest(new { error = "This invitation is invalid or has expired." });

        var user = await _db.Users.FirstAsync(u => u.Id == User.GetUserId());
        if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
            return Denied("This invitation was issued to a different email address.");

        var already = await _db.Memberships
            .AnyAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == user.Id);
        if (already)
        {
            invite.Status = InvitationStatus.Accepted;
            invite.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Conflict(new { error = "You are already a member of this organization." });
        }

        _db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = invite.OrganizationId,
            Role = invite.Role,
            Status = MembershipStatus.Active
        });
        invite.Status = InvitationStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private ObjectResult Denied(string error) =>
        StatusCode(StatusCodes.Status403Forbidden, new { error });
}
