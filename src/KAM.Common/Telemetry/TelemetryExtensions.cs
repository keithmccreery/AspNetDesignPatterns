using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KAM.Common.Telemetry;

/// <summary>
/// Wires ASP.NET Core + <see cref="HttpClient"/> + runtime instrumentation to an OTLP exporter,
/// for traces and metrics. Kept separate from logging: Serilog stays the app's
/// <c>ILoggerFactory</c> everywhere else (see <c>CLAUDE.md</c>'s "<c>ILogger&lt;T&gt;</c> only"
/// rule), so the log side is one more <c>WriteTo.OpenTelemetry(...)</c> sink added directly to
/// <c>Program.cs</c>'s existing <c>AddSerilog(...)</c> call, sharing the same
/// <see cref="TelemetrySettings.OtlpEndpoint"/> passed in here.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Registers tracing and metrics, exported via OTLP, when <paramref name="settings"/> says
    /// to. A no-op (returns <paramref name="services"/> unchanged) when
    /// <see cref="TelemetrySettings.Enabled"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">
    /// This app's own name, shown as the service in the collector/dashboard. Never a default
    /// owned by this library — <c>KAM.Common</c> has no identity of its own to report, the same
    /// reasoning <c>OpenApiExtensions.MapApiReference</c>'s <c>title</c> parameter follows.
    /// </param>
    /// <param name="settings">Export configuration — see <see cref="TelemetrySettings"/>.</param>
    public static IServiceCollection AddAppTelemetry(
        this IServiceCollection services,
        string serviceName,
        TelemetrySettings settings)
    {
        if (!settings.Enabled)
        {
            return services;
        }

        void ConfigureOtlpExporter(OtlpExporterOptions options)
        {
            if (settings.OtlpEndpoint is not null)
            {
                options.Endpoint = new Uri(settings.OtlpEndpoint);
            }
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySources.Pipeline.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ConfigureOtlpExporter))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ConfigureOtlpExporter));

        return services;
    }
}
