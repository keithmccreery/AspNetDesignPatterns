using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
    public async Task<OpenMeteoForecast?> GetForecastAsync(
        double latitude,
        double longitude,
        int days,
        CancellationToken cancellationToken)
    {
        string query =
            $"/v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum" +
            $"&forecast_days={days.ToString(CultureInfo.InvariantCulture)}&timezone=UTC";

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(query, cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                logger.LogWarning("open-meteo rejected the request for {Lat},{Lon}", latitude, longitude);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<OpenMeteoForecast>(jsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new WeatherClientException("Failed to retrieve the forecast from open-meteo.", ex);
        }
    }
}
