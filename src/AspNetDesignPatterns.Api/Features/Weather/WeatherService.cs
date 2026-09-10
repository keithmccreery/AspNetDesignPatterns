namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// The first layer above the client. Turns whatever the client does — a value, a
/// <see langword="null"/>, or a <see cref="WeatherClientException"/> — into a
/// <see cref="Result{T}"/>, and maps the upstream shape to the API contract.
/// </summary>
/// <remarks>
/// Takes no <c>ILogger</c>: on failure it attaches the exception and request context to the
/// <see cref="Error"/> and lets the boundary log it (<c>result.LogOnFailure(logger)</c>).
/// </remarks>
internal sealed class WeatherService(IWeatherClient client)
{
    public async Task<Result<ForecastResponse>> GetForecastAsync(
        GetForecastRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenMeteoForecast? upstream = await client.GetForecastAsync(
                request.Latitude, request.Longitude, request.Days, cancellationToken);

            if (upstream?.Daily is null)
            {
                return Error.NotFound(
                    "Weather.LocationNotFound",
                    "No forecast is available for the supplied coordinates.");
            }

            return Map(upstream);
        }
        catch (WeatherClientException ex)
        {
            return Error
                .Upstream("Weather.ProviderUnavailable", "The weather provider is currently unavailable. Please retry shortly.")
                .WithException(ex)
                .WithContext(new { request.Latitude, request.Longitude, request.Days });
        }
    }

    private static ForecastResponse Map(OpenMeteoForecast upstream)
    {
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
