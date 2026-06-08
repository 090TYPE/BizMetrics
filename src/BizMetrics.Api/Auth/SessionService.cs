using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace BizMetrics.Api.Auth;

/// <summary>
/// Issues an access/refresh token pair and persists the refresh token. Shared by
/// the auth flow (register/login/refresh) and the org switcher, so token issuance
/// lives in exactly one place.
/// </summary>
public class SessionService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwt;

    public SessionService(AppDbContext db, ITokenService tokens, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
    }

    public async Task<AuthResponse> IssueAsync(User user, Guid? orgId, OrgRole? role)
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
}
