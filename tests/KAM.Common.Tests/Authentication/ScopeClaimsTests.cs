using System.Security.Claims;

using KAM.Common.Authentication;

namespace KAM.Common.Tests.Authentication;

[TestFixture]
public class ScopeClaimsTests
{
    private static ClaimsPrincipal UserWith(params string[] scopeClaimValues) =>
        new(new ClaimsIdentity(scopeClaimValues.Select(v => new Claim("scope", v)), "test"));

    [Test]
    public void Matches_an_exact_single_scope_claim()
    {
        // Arrange
        ClaimsPrincipal user = UserWith("weather:read");

        // Act & Assert
        ScopeClaims.Has(user, "weather:read").Should().BeTrue();
    }

    [Test]
    public void Matches_a_scope_within_a_space_delimited_claim()
    {
        // Arrange — the standard OAuth2/OIDC shape: one claim, several scopes.
        ClaimsPrincipal user = UserWith("openid weather:read profile");

        // Act & Assert
        ScopeClaims.Has(user, "weather:read").Should().BeTrue();
    }

    [Test]
    public void Matches_across_multiple_discrete_scope_claims()
    {
        // Arrange — what DevTokenIssuer used to emit, and some IdPs configured for array claims.
        ClaimsPrincipal user = UserWith("openid", "weather:read");

        // Act & Assert
        ScopeClaims.Has(user, "weather:read").Should().BeTrue();
    }

    [Test]
    public void Does_not_match_a_scope_the_user_does_not_have()
    {
        // Arrange
        ClaimsPrincipal user = UserWith("openid profile");

        // Act & Assert
        ScopeClaims.Has(user, "weather:read").Should().BeFalse();
    }

    [Test]
    public void Does_not_match_when_there_is_no_scope_claim_at_all()
    {
        // Arrange
        ClaimsPrincipal user = new(new ClaimsIdentity("test"));

        // Act & Assert
        ScopeClaims.Has(user, "weather:read").Should().BeFalse();
    }
}
