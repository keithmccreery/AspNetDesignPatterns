namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// The Weather slice's authorization policy names. Endpoints reference these by constant,
/// never by string literal. Each policy is defined in configuration
/// (<c>Authorization:Policies:&lt;name&gt;</c> in <c>appsettings.json</c>), not here — this is only
/// the name the endpoint and the config agree on.
/// </summary>
/// <remarks>
/// Lives in the slice, not <c>KAM.Common</c>: which policies and scopes exist is this app's
/// business, and the shared library has no identity of its own to lend them.
/// </remarks>
internal static class WeatherPolicies
{
    /// <summary>Read access to weather data. Configured to require the <see cref="WeatherScopes.Read"/> scope.</summary>
    public const string Read = "WeatherRead";
}

/// <summary>
/// The OAuth2/OIDC scope values the Weather slice's policies check, kept deliberately separate
/// from the policy names in <see cref="WeatherPolicies"/>: a policy name flows through
/// <c>IConfiguration</c>, where <c>:</c> is the path separator, so it must avoid the <c>:</c>
/// a scope value uses freely (see <c>KAM.Common/Authorization/README.md</c>).
/// </summary>
internal static class WeatherScopes
{
    /// <summary>Grants read access to weather data.</summary>
    public const string Read = "weather:read";
}
