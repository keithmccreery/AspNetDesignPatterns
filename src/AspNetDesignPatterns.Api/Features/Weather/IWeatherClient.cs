namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Low-level access to the upstream weather provider. This is the one layer that is allowed
/// to <em>throw</em>: transport and deserialization failures surface as
/// <see cref="WeatherClientException"/>, which <see cref="WeatherService"/> converts to a
/// <c>Result</c>.
/// </summary>
public interface IWeatherClient
{
    /// <returns>The upstream forecast, with a well-formed <see cref="OpenMeteoForecast.Daily"/>.</returns>
    /// <exception cref="WeatherClientException">The upstream call failed, or returned an unusable payload.</exception>
    Task<OpenMeteoForecast> GetForecastAsync(
        double latitude,
        double longitude,
        int days,
        CancellationToken cancellationToken);
}

/// <summary>
/// Raised when the upstream weather provider cannot be reached, rejects the request, or returns
/// an unusable payload. <paramref name="innerException"/> is the originating transport/
/// deserialization failure when there is one; a structurally invalid-but-parseable payload
/// (no, mismatched or missing daily arrays) has none, so it is optional.
/// </summary>
public sealed class WeatherClientException(string message, Exception? innerException = null)
    : Exception(message, innerException);
