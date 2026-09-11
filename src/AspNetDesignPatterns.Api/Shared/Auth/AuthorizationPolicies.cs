namespace AspNetDesignPatterns.Api.Shared.Auth;

/// <summary>
/// Named authorization policies. Endpoints reference these by constant, never by string
/// literal. A policy name is an internal identifier — it happens to read the same as the
/// scope it checks, but the two are deliberately separate constants (see <see cref="Scopes"/>)
/// so renaming one doesn't silently change the other.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Read access to weather data. Requires the <see cref="Scopes.WeatherRead"/> scope.</summary>
    public const string WeatherRead = "weather:read";
}

/// <summary>
/// The OAuth2/OIDC scope values this API recognises, independent of the policy names that
/// check them. Checked via <see cref="ScopeClaims"/>, which understands both shapes an IdP
/// might send a scope in.
/// </summary>
public static class Scopes
{
    /// <summary>Grants read access to weather data.</summary>
    public const string WeatherRead = "weather:read";

    /// <summary>The scopes <see cref="DevTokenIssuer"/> grants a locally-minted token.</summary>
    public static readonly string[] Default = [WeatherRead];
}
