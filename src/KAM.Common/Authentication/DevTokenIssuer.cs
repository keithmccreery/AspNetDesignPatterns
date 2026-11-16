using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KAM.Common.Authentication;

/// <summary>
/// Mints signed HS256 JWTs from <see cref="JwtOptions"/>. Used only by the Development-only
/// dev-token endpoint; extracted from the endpoint so the token-shaping logic is unit-testable
/// without a web host.
/// </summary>
internal sealed class DevTokenIssuer(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public TokenResponse Issue(string subject)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(Claims(subject)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResponse(token, expires);
    }

    private IEnumerable<Claim> Claims(string subject)
    {
        yield return new Claim(JwtRegisteredClaimNames.Sub, subject);
        yield return new Claim(JwtRegisteredClaimNames.Name, subject); // the raw "name" claim a real IdP sends

        if (_jwt.DevTokenScopes.Count > 0)
        {
            // Standard OAuth2 shape: one space-delimited claim.
            yield return new Claim("scope", string.Join(' ', _jwt.DevTokenScopes));
        }
    }
}
