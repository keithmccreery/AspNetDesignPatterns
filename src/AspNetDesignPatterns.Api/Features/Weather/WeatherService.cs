namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// <see cref="IWeatherService"/>: calls the client, maps the upstream shape to the API contract,
/// and turns a <see cref="WeatherClientException"/> into
/// <see cref="WeatherErrors.ProviderUnavailable"/> with <see cref="Result.TryAsync{T, TException}"/>.
/// </summary>
/// <remarks>
/// Takes no <c>ILogger</c>: on failure it attaches the exception and request context to the
/// <see cref="Error"/> and lets the boundary log it (<c>result.LogOnFailure(logger)</c>).
/// </remarks>
internal sealed class WeatherService(IWeatherClient client) : IWeatherService
{
    public Task<Result<ForecastResponse>> GetForecastAsync(
        GetForecastRequest request,
        CancellationToken cancellationToken) =>
        Result.TryAsync(
            async () => Result.Success(Map(await client.GetForecastAsync(
                request.Latitude, request.Longitude, request.Days, cancellationToken))),
            (WeatherClientException ex) => WeatherErrors.ProviderUnavailable
                .WithException(ex)
                .WithContext(new { request.Latitude, request.Longitude, request.Days }));

    private static ForecastResponse Map(OpenMeteoForecast upstream)
    {
        // Non-null by IWeatherClient's contract -- the client throws rather than return one without.
        OpenMeteoDaily daily = upstream.Daily!;
        List<DailyForecast> days = daily.Time
            .Select((date, i) => new DailyForecast(
                date,
                daily.TemperatureMax[i],
                daily.TemperatureMin[i],
                daily.PrecipitationSum[i]))
            .ToList();

        return new ForecastResponse(upstream.Latitude, upstream.Longitude, upstream.Timezone, days);
    }
}
