using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

using Polly;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Typed <see cref="HttpClient"/> for open-meteo. Resilience (retry / circuit breaker /
/// timeout) is attached to the client registration in <see cref="WeatherDependencies"/>,
/// not here.
/// </summary>
internal sealed class OpenMeteoWeatherClient(
    HttpClient httpClient,
    JsonSerializerOptions jsonOptions) : IWeatherClient
{
    private static readonly string DailyFields =
        string.Join(',', "temperature_2m_max", "temperature_2m_min", "precipitation_sum");

    public async Task<OpenMeteoForecast> GetForecastAsync(
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

            // Every non-2xx is a provider failure, 400 included: the request validator already
            // bounds every parameter, so a 400 means open-meteo rejected a request *we* built
            // (our bug, or an upstream contract change) -- never "the caller's input was wrong",
            // and open-meteo has no "unknown location" response to translate into a 404.
            response.EnsureSuccessStatusCode();

            OpenMeteoForecast? forecast = await response.Content.ReadFromJsonAsync<OpenMeteoForecast>(jsonOptions, cancellationToken);

            if (forecast?.Daily is not { } daily || !IsWellFormed(daily))
            {
                // Parses fine but the shape is unusable -- a null body, or a missing or short
                // array, would otherwise surface as an unhandled NullReferenceException/
                // IndexOutOfRangeException several layers up, in WeatherService.Map. Catching it
                // here keeps "only the client throws" true: this is a deserialization-boundary
                // failure like any other.
                throw new WeatherClientException("open-meteo returned no daily forecast, or one with missing or mismatched fields.");
            }

            return forecast;
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException or ExecutionRejectedException)
            && !cancellationToken.IsCancellationRequested)
        {
            // ExecutionRejectedException is the base of what the global resilience handler
            // throws *instead of* calling open-meteo: BrokenCircuitException (circuit open),
            // TimeoutRejectedException (Polly attempt/total timeout) and
            // RateLimiterRejectedException. None of them is an HttpRequestException, so without
            // this they'd escape as a 500 -- exactly when the provider is most unavailable.
            //
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
