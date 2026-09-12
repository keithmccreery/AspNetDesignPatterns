using System.Reflection;

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
/// <see cref="TelemetrySettings.OtlpEndpoint"/> and calling <see cref="ResolveServiceName"/> /
/// <see cref="ResolveServiceVersion"/> so logs and traces/metrics can never disagree on which
/// service produced them.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Registers tracing and metrics, exported via OTLP, when <paramref name="settings"/> says
    /// to. A no-op (returns <paramref name="services"/> unchanged) when
    /// <see cref="TelemetrySettings.Enabled"/> is <see langword="false"/>.
    /// </summary>
    public static IServiceCollection AddAppTelemetry(this IServiceCollection services, TelemetrySettings settings)
    {
        if (!settings.Enabled)
        {
            return services;
        }

        string serviceName = ResolveServiceName(settings);
        string? serviceVersion = ResolveServiceVersion(settings);

        void ConfigureOtlpExporter(OtlpExporterOptions options)
        {
            if (settings.OtlpEndpoint is not null)
            {
                options.Endpoint = new Uri(settings.OtlpEndpoint);
            }
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
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

    /// <summary>
    /// <see cref="TelemetrySettings.ServiceName"/> if set; otherwise the entry assembly's own
    /// name — never a default owned by this library, since <c>KAM.Common</c> has no identity of
    /// its own to report (the same reasoning <c>OpenApiExtensions.MapApiReference</c>'s
    /// <c>title</c> parameter follows, just resolved automatically instead of requiring a
    /// caller-supplied literal, since a sensible default genuinely exists here).
    /// </summary>
    public static string ResolveServiceName(TelemetrySettings settings) =>
        settings.ServiceName
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? "UnknownService";

    /// <summary>
    /// <see cref="TelemetrySettings.ServiceVersion"/> if set; otherwise the entry assembly's
    /// informational version (its <c>&lt;Version&gt;</c>, plus a source-link commit hash on
    /// deterministic builds) — or <see langword="null"/> if neither is available.
    /// </summary>
    public static string? ResolveServiceVersion(TelemetrySettings settings) =>
        settings.ServiceVersion
        ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
