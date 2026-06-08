using System.IdentityModel.Tokens.Jwt;
using BizMetrics.Api.Auth;
using BizMetrics.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace BizMetrics.Tests;

public class TokenServiceTests
{
    private static TokenService NewService() => new(Options.Create(new JwtOptions
    {
        Issuer = "test",
        Audience = "test",
        SigningKey = "test-signing-key-that-is-long-enough-32b!",
        AccessTokenMinutes = 15
    }));

    [Fact]
    public void Access_token_carries_user_org_and_role_claims()
    {
        var svc = NewService();
        var user = new User { Email = "a@b.com" };
        var orgId = Guid.NewGuid();

        var jwt = svc.CreateAccessToken(user, orgId, OrgRole.Owner);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Equal(user.Id.ToString(), parsed.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(orgId.ToString(), parsed.Claims.First(c => c.Type == "org_id").Value);
        Assert.Equal("Owner", parsed.Claims.First(c => c.Type == "org_role").Value);
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_not_the_raw_token()
    {
        var svc = NewService();
        var (token, hash) = svc.CreateRefreshToken();

        Assert.NotEqual(token, hash);
        Assert.Equal(hash, svc.HashRefreshToken(token));
    }
}
