namespace AspNetDesignPatterns.Api.Shared.Auth;

/// <summary>Named authorization policies. Endpoints reference these by constant, never by string literal.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Read access to weather data. Requires an authenticated user with the <c>weather:read</c> scope.</summary>
    public const string WeatherRead = "weather:read";

    /// <summary>The scope claim value the dev token endpoint issues.</summary>
    public static readonly string[] DefaultScopes = ["weather:read"];
}
