using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Typed <see cref="HttpClient"/> for open-meteo. Resilience (retry / circuit breaker /
/// timeout) is attached to the client registration in <see cref="WeatherDependencies"/>,
/// not here.
/// </summary>
internal sealed class OpenMeteoWeatherClient(
    HttpClient httpClient,
    JsonSerializerOptions jsonOptions,
    ILogger<OpenMeteoWeatherClient> logger) : IWeatherClient
{
    private static readonly string DailyFields =
        string.Join(',', "temperature_2m_max", "temperature_2m_min", "precipitation_sum");

    public async Task<OpenMeteoForecast?> GetForecastAsync(
        double latitude,
        double longitude,
        int days,
        CancellationToken cancellationToken)
    {
        // QueryHelpers URL-encodes every value -- these are all numeric today, but it's the
        // pattern to copy if a future parameter is free text.
        string query = QueryHelpers.AddQueryString("/v1/forecast", new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["latitude"] = latitude.ToString(CultureInfo.InvariantCulture),
            ["longitude"] = longitude.ToString(CultureInfo.InvariantCulture),
            ["daily"] = DailyFields,
            ["forecast_days"] = days.ToString(CultureInfo.InvariantCulture),
            ["timezone"] = "UTC",
        });

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(query, cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                logger.LogWarning("open-meteo rejected the request for {Lat},{Lon}", latitude, longitude);
                return null;
            }

            response.EnsureSuccessStatusCode();

            OpenMeteoForecast? forecast = await response.Content.ReadFromJsonAsync<OpenMeteoForecast>(jsonOptions, cancellationToken);

            if (forecast is { Daily: { } daily } && !IsWellFormed(daily))
            {
                // Parses fine but the shape is unusable -- a missing or short array would
                // otherwise surface as an unhandled NullReferenceException/IndexOutOfRangeException
                // several layers up, in WeatherService.Map. Catching it here keeps "only the
                // client throws" true: this is a deserialization-boundary failure like any other.
                throw new WeatherClientException("open-meteo returned a daily forecast with missing or mismatched fields.");
            }

            return forecast;
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException)
            && !cancellationToken.IsCancellationRequested)
        {
            // Excludes the case where the *caller* cancelled: HttpClient reports both a timeout
            // and a caller-initiated cancellation as TaskCanceledException, and only the former
            // is "the provider is unavailable" -- a cancelled request should propagate as
            // cancellation, not become a misleading 502.
            throw new WeatherClientException("Failed to retrieve the forecast from open-meteo.", ex);
        }
    }

    private static bool IsWellFormed(OpenMeteoDaily daily) =>
        daily is { Time: not null, TemperatureMax: not null, TemperatureMin: not null, PrecipitationSum: not null }
        && daily.TemperatureMax.Count == daily.Time.Count
        && daily.TemperatureMin.Count == daily.Time.Count
        && daily.PrecipitationSum.Count == daily.Time.Count;
}
