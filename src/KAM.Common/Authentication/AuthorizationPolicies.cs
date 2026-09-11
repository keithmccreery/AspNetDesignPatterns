namespace KAM.Common.Authentication;

/// <summary>
/// Named authorization policies. Endpoints reference these by constant, never by string
/// literal. A policy name is an internal identifier, deliberately separate from the scope
/// value it checks (see <see cref="Scopes"/>) — so renaming one doesn't silently change the
/// other, and so the name is free to avoid characters (like <c>:</c>) that mean something
/// different to configuration binding than they do in a scope string. This one is
/// config-driven — see <c>Authorization:Policies:WeatherRead</c> in <c>appsettings.json</c>
/// and <see cref="KAM.Common.Authorization.AuthorizationExtensions"/>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Read access to weather data. Configured to require the <see cref="Scopes.WeatherRead"/> scope.</summary>
    public const string WeatherRead = "WeatherRead";
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
