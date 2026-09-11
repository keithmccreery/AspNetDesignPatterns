using System.Security.Claims;

namespace KAM.Common.Authentication;

/// <summary>
/// Reads OAuth2/OIDC <c>scope</c> claims. A <c>RequireClaim("scope", value)</c> policy only
/// matches a claim whose value is <em>exactly</em> the required scope — real identity
/// providers (Entra ID, Auth0, Keycloak, …) emit one space-delimited <c>scope</c> claim per
/// the OAuth2 spec (e.g. <c>"weather:read openid profile"</c>), which that check rejects.
/// This handles the standard space-delimited shape and a discrete claim per scope (what
/// <see cref="DevTokenIssuer"/> — and some IdPs configured for array-valued claims — emit).
/// </summary>
public static class ScopeClaims
{
    private const string CLAIM_TYPE = "scope";

    /// <summary>Whether <paramref name="user"/> carries <paramref name="scope"/> in any <c>scope</c> claim.</summary>
    public static bool Has(ClaimsPrincipal user, string scope) =>
        user.FindAll(CLAIM_TYPE)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
