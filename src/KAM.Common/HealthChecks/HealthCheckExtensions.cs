using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace KAM.Common.HealthChecks;

/// <summary>
/// Maps the two health probes. The health-check <em>services</em> are enabled by a bare
/// <c>builder.Services.AddHealthChecks()</c> in <c>Program.cs</c> (framework API, like
/// <c>AddHttpContextAccessor()</c>); individual checks self-register from the slice that owns
/// them — see <c>Features/Weather/WeatherProviderHealthCheck.cs</c>.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds <c>/health/live</c> and <c>/health/ready</c>. Both are anonymous, both are hidden
    /// from the OpenAPI document, and both render through <see cref="HealthCheckResponseWriter"/>.
    /// </summary>
    public static WebApplication MapAppHealthChecks(this WebApplication app)
    {
        // Liveness: run NO checks. A 200 just means the process is responsive and its request
        // pipeline is not wedged. Dependency health is deliberately out of scope here — a
        // downstream outage must not make the orchestrator kill and restart the pod.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        })
        .WithName("HealthLive")
        .AllowAnonymous()
        .ExcludeFromDescription();

        // Readiness: run every check tagged "ready" — the dependencies that must be reachable
        // before this instance should receive traffic. Healthy/Degraded → 200, Unhealthy → 503.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTag.Ready),
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        })
        .WithName("HealthReady")
        .AllowAnonymous()
        .ExcludeFromDescription();

        return app;
    }
}
