namespace AspNetDesignPatterns.Api.Shared.Auth;

/// <summary>Request body for the Development-only dev-token endpoint.</summary>
/// <param name="Subject">The user name to embed as the token subject.</param>
public sealed record TokenRequest(string Subject);

/// <summary>A freshly minted bearer token.</summary>
/// <param name="AccessToken">The signed JWT.</param>
/// <param name="ExpiresAtUtc">Absolute expiry.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string TokenType = "Bearer");
