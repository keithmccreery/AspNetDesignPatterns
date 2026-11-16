namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Every <see cref="Error"/> the Weather slice can return, in one place: the codes are part of
/// the API's contract (they reach the client in <c>ProblemDetails</c> and are what logs and
/// tests match on), so each is defined once rather than as a string literal at the call site.
/// </summary>
/// <remarks>
/// <see cref="Error"/> is an immutable record, so sharing one instance is safe:
/// <see cref="Error.WithException"/> and <see cref="Error.WithContext"/> return copies.
/// </remarks>
internal static class WeatherErrors
{
    /// <summary>The weather provider failed or returned something unusable (HTTP 502).</summary>
    public static readonly Error ProviderUnavailable = Error.Upstream(
        "Weather.ProviderUnavailable",
        "The weather provider is currently unavailable. Please retry shortly.");

    /// <summary>
    /// The forecast pipeline succeeded without setting a response — a bug in a pipeline step,
    /// not something a caller can cause (HTTP 500).
    /// </summary>
    public static readonly Error NoResponse = Error.Failure(
        "Weather.NoResponse",
        "The forecast pipeline completed without producing a response.");
}
