namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Low-level access to the upstream weather provider. This is the one layer that is allowed
/// to <em>throw</em>: transport and deserialization failures surface as
/// <see cref="WeatherClientException"/>, which <see cref="WeatherService"/> converts to a
/// <c>Result</c>.
/// </summary>
public interface IWeatherClient
{
    /// <returns>The upstream forecast, or <see langword="null"/> if the location is unknown.</returns>
    /// <exception cref="WeatherClientException">The upstream call failed.</exception>
    Task<OpenMeteoForecast?> GetForecastAsync(
        double latitude,
        double longitude,
        int days,
        CancellationToken cancellationToken);
}

/// <summary>Raised when the upstream weather provider cannot be reached or returns an unusable payload.</summary>
public sealed class WeatherClientException(string message, Exception innerException)
    : Exception(message, innerException);
