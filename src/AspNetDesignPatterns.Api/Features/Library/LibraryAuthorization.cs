namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The Library slice's authorization policy names. Each policy is defined in configuration
/// (<c>Authorization:Policies:&lt;name&gt;</c> in <c>appsettings.json</c>); this is only the name the
/// endpoints and the config agree on. See <c>WeatherAuthorization.cs</c> for why policy names
/// and scope values are separate constants.
/// </summary>
internal static class LibraryPolicies
{
    /// <summary>Browse books and read your own loans. Requires <see cref="LibraryScopes.Read"/>.</summary>
    public const string Read = "LibraryRead";

    /// <summary>Borrow and return books. Requires <see cref="LibraryScopes.Write"/>.</summary>
    public const string Write = "LibraryWrite";
}

/// <summary>The OAuth2/OIDC scope values the Library slice's policies check.</summary>
internal static class LibraryScopes
{
    public const string Read = "library:read";

    public const string Write = "library:write";
}
