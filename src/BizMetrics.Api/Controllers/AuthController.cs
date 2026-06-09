using System.Text.RegularExpressions;
using BizMetrics.Api.Auth;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Audit;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Audit;
using BizMetrics.Infrastructure.Persistence;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BizMetrics.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]  // brute-force protection: 10 req/min per IP
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly SessionService _sessions;
    private readonly AuditService _audit;
    private readonly GoogleOptions _google;

    public AuthController(
        AppDbContext db, ITokenService tokens, SessionService sessions,
        AuditService audit, IOptions<GoogleOptions> google)
    {
        _db = db;
        _tokens = tokens;
        _sessions = sessions;
        _audit = audit;
        _google = google.Value;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Registers a user, creates their organization, and starts a 14-day trial.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "Email already registered." });

        var user = new User
        {
            Email = email,
            FullName = req.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        var org = new Organization
        {
            Name = req.OrganizationName.Trim(),
            Slug = await UniqueSlugAsync(req.OrganizationName),
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(14)
        };

        var membership = new Membership
        {
            User = user,
            Organization = org,
            Role = OrgRole.Owner,
            Status = MembershipStatus.Active
        };

        _db.Users.Add(user);
        _db.Organizations.Add(org);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync();

        var response = await _sessions.IssueAsync(user, org.Id, OrgRole.Owner);

        await _audit.LogAsync(user.Id, AuditActions.UserRegistered, "User",
            user.Id.ToString(), new { email, org = org.Name },
            orgIdOverride: org.Id, ipAddress: ClientIp);

        return response;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            // Audit failed login (without tenant context — no org yet)
            // We skip the audit here since we don't have an org to anchor it to
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var membership = await _db.Memberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        var response = await _sessions.IssueAsync(user, membership?.OrganizationId, membership?.Role);

        if (membership is not null)
            await _audit.LogAsync(user.Id, AuditActions.UserLoggedIn, "User",
                user.Id.ToString(), null,
                orgIdOverride: membership.OrganizationId, ipAddress: ClientIp);

        return response;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var hash = _tokens.HashRefreshToken(req.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (existing is null || !existing.IsActive)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        existing.RevokedAt = DateTime.UtcNow;

        var membership = await _db.Memberships
            .Where(m => m.UserId == existing.UserId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        return await _sessions.IssueAsync(existing.User, membership?.OrganizationId, membership?.Role);
    }

    // ── Google OAuth ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies a Google ID token from the frontend and returns a JWT pair.
    /// On first sign-in, creates a user and organization automatically.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest req)
    {
        if (!_google.IsConfigured)
            return StatusCode(503, new { error = "Google sign-in is not configured on this instance." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_google.ClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(req.IdToken, settings);
        }
        catch
        {
            return Unauthorized(new { error = "Invalid Google token." });
        }

        var email = payload.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { error = "Google account has no email address." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        Organization? org = null;
        var isNew = user is null;

        if (user is null)
        {
            // First sign-in: create user + org
            user = new User
            {
                Email = email,
                FullName = payload.Name ?? email,
                PasswordHash = string.Empty   // no password for OAuth users
            };
            org = new Organization
            {
                Name = $"{user.FullName}'s Workspace",
                Slug = await UniqueSlugAsync(user.FullName),
                SubscriptionStatus = SubscriptionStatus.Trialing,
                TrialEndsAt = DateTime.UtcNow.AddDays(14)
            };
            _db.Users.Add(user);
            _db.Organizations.Add(org);
            _db.Memberships.Add(new Membership
            {
                User = user, Organization = org,
                Role = OrgRole.Owner, Status = MembershipStatus.Active
            });
            await _db.SaveChangesAsync();
        }

        var membership = await _db.Memberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        var response = await _sessions.IssueAsync(user, membership?.OrganizationId, membership?.Role);

        var auditAction = isNew ? AuditActions.UserRegisteredViaGoogle : AuditActions.UserLoggedInGoogle;
        if (membership is not null)
            await _audit.LogAsync(user.Id, auditAction, "User", user.Id.ToString(),
                new { email }, orgIdOverride: membership.OrganizationId, ipAddress: ClientIp);

        return response;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<string> UniqueSlugAsync(string name)
    {
        var baseSlug = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "org";

        var slug = baseSlug;
        var i = 1;
        while (await _db.Organizations.AnyAsync(o => o.Slug == slug))
            slug = $"{baseSlug}-{++i}";
        return slug;
    }
}
