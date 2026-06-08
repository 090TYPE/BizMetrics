using System.Text.RegularExpressions;
using BizMetrics.Api.Auth;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BizMetrics.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwt;

    public AuthController(AppDbContext db, ITokenService tokens, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
    }

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

        return await IssueTokensAsync(user, org.Id, OrgRole.Owner);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        // Pick the user's first active org as the working tenant. A real org
        // switcher comes in Phase 1; for now login lands you in one org.
        var membership = await _db.Memberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        return await IssueTokensAsync(user, membership?.OrganizationId, membership?.Role);
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

        // Rotate: revoke the presented token, issue a fresh pair.
        existing.RevokedAt = DateTime.UtcNow;

        var membership = await _db.Memberships
            .Where(m => m.UserId == existing.UserId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        return await IssueTokensAsync(existing.User, membership?.OrganizationId, membership?.Role);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Guid? orgId, OrgRole? role)
    {
        var access = _tokens.CreateAccessToken(user, orgId, role);
        var (refresh, refreshHash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(access, refresh, user.Id, orgId, role?.ToString());
    }

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
