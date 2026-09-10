using System.Text.Json.Serialization;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// The subset of the open-meteo <c>/v1/forecast</c> response we consume. Lives at the client
/// boundary only; <see cref="WeatherService"/> maps it to <see cref="ForecastResponse"/>.
/// </summary>
public sealed record OpenMeteoForecast(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("daily")] OpenMeteoDaily? Daily);

public sealed record OpenMeteoDaily(
    [property: JsonPropertyName("time")] IReadOnlyList<DateOnly> Time,
    [property: JsonPropertyName("temperature_2m_max")] IReadOnlyList<double> TemperatureMax,
    [property: JsonPropertyName("temperature_2m_min")] IReadOnlyList<double> TemperatureMin,
    [property: JsonPropertyName("precipitation_sum")] IReadOnlyList<double> PrecipitationSum);
