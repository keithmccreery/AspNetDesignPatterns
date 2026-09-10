using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Readiness check for the open-meteo dependency. Reports <see cref="HealthStatus.Degraded"/>
/// (not <see cref="HealthStatus.Unhealthy"/>) when the provider is down: the forecast endpoint
/// degrades gracefully to a 502 <c>ProblemDetails</c> and the rest of the API keeps working,
/// so the instance is still "ready" — the check just surfaces the problem in the report.
/// </summary>
/// <remarks>
/// Uses its own <see cref="HttpClient"/> (registered in <see cref="WeatherDependencies"/> with
/// the resilience handler removed and a short timeout) so a probe is one fast request, not a
/// retrying Polly pipeline.
/// </remarks>
internal sealed class WeatherProviderHealthCheck(HttpClient httpClient) : IHealthCheck
{
    // A minimal but valid forecast request: null island, one day. We only inspect the status.
    private const string PROBE_PATH = "/v1/forecast?latitude=0&longitude=0&forecast_days=1&timezone=UTC";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(PROBE_PATH, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"open-meteo responded {response.StatusCode}.")
                : HealthCheckResult.Degraded($"open-meteo responded {response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Degraded("open-meteo is unreachable.", ex);
        }
    }
}
