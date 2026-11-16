namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// The Weather slice's service layer: the first layer above <see cref="IWeatherClient"/>. Turns
/// what the client does (a value or a <see cref="WeatherClientException"/>) into a
/// <see cref="Result{T}"/>, so nothing above it handles exceptions.
/// </summary>
internal interface IWeatherService
{
    /// <summary>
    /// The forecast mapped to the API contract, or <see cref="WeatherErrors.ProviderUnavailable"/>
    /// when the provider fails.
    /// </summary>
    Task<Result<ForecastResponse>> GetForecastAsync(GetForecastRequest request, CancellationToken cancellationToken);
}
