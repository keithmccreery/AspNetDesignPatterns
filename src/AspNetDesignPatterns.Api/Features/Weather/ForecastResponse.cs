namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>The API's forecast contract — deliberately decoupled from the upstream provider's shape.</summary>
public sealed record ForecastResponse(
    double Latitude,
    double Longitude,
    string Timezone,
    IReadOnlyList<DailyForecast> Days);

public sealed record DailyForecast(
    DateOnly Date,
    double TemperatureMaxC,
    double TemperatureMinC,
    double PrecipitationMm);
