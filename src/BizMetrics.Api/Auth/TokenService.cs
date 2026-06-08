using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BizMetrics.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BizMetrics.Api.Auth;

public interface ITokenService
{
    /// <summary>Issues a signed access JWT carrying the user id and (optionally) the active org.</summary>
    string CreateAccessToken(User user, Guid? organizationId, OrgRole? role);

    /// <summary>Returns a cryptographically random refresh token and its storage hash.</summary>
    (string token, string hash) CreateRefreshToken();

    string HashRefreshToken(string token);

    /// <summary>A random URL-safe opaque token plus its storage hash (e.g. invite links).</summary>
    (string token, string hash) CreateOpaqueToken();

    string HashToken(string token);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _opt;

    public TokenService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public string CreateAccessToken(User user, Guid? organizationId, OrgRole? role)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (organizationId.HasValue)
            claims.Add(new Claim("org_id", organizationId.Value.ToString()));
        if (role.HasValue)
            claims.Add(new Claim("org_role", role.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string token, string hash) CreateRefreshToken() => CreateOpaqueToken();

    public string HashRefreshToken(string token) => HashToken(token);

    public (string token, string hash) CreateOpaqueToken()
    {
        // URL-safe base64 so the raw token can sit in an invite link unescaped.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, HashToken(token));
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
