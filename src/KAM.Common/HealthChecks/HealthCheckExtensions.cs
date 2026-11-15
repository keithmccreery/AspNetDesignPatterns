using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
    /// <param name="app">The application to map the probes onto.</param>
    /// <param name="includeDetails">
    /// Whether each entry's description and exception message are written. Required, not
    /// defaulted: the routes are anonymous and <c>Exception.Message</c> can leak a hostname or
    /// connection-string fragment, so the caller states the policy (typically
    /// <c>!app.Environment.IsProduction()</c>).
    /// </param>
    public static WebApplication MapAppHealthChecks(this WebApplication app, bool includeDetails)
    {
        Task WriteResponseAsync(HttpContext context, HealthReport report) =>
            HealthCheckResponseWriter.WriteAsync(context, report, includeDetails);

        // Liveness: run NO checks. A 200 just means the process is responsive and its request
        // pipeline is not wedged. Dependency health is deliberately out of scope here — a
        // downstream outage must not make the orchestrator kill and restart the pod.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        })
        .WithName("HealthLive")
        .AllowAnonymous()
        .ExcludeFromDescription();

        // Readiness: run every check tagged "ready" — the dependencies that must be reachable
        // before this instance should receive traffic. Healthy/Degraded → 200, Unhealthy → 503.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTag.Ready),
            ResponseWriter = WriteResponseAsync,
        })
        .WithName("HealthReady")
        .AllowAnonymous()
        .ExcludeFromDescription();

        return app;
    }
}
