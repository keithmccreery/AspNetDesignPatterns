using KAM.Common.DependencyInjection;

namespace KAM.Common.Telemetry;

/// <summary>
/// OpenTelemetry export settings, bound from the "Telemetry" configuration section. Governs
/// traces, metrics, and the extra Serilog log sink together — see
/// <see cref="TelemetryExtensions.AddAppTelemetry"/> and <c>Program.cs</c>'s <c>AddSerilog</c>
/// call, both of which read <see cref="OtlpEndpoint"/> so one config value drives all three
/// signals to the same collector.
/// </summary>
public sealed class TelemetrySettings : SettingsBase<TelemetrySettings>
{
    public static string Section => "Telemetry";

    /// <summary>
    /// Whether to export traces/metrics/logs at all. Left on by default — the OTLP exporter
    /// fails silently (batches are dropped, nothing throws into the request pipeline) when
    /// nothing is listening at <see cref="OtlpEndpoint"/>, so there's no cost to leaving this on
    /// with no collector running.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The OTLP endpoint traces, metrics, and logs are all sent to. Unset means each exporter's
    /// own default (<c>http://localhost:4317</c>, OTLP/gRPC) — which is exactly the port the
    /// .NET Aspire Dashboard container listens on, so local dev needs no config at all.
    /// </summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary>
    /// The reported <c>service.name</c>. Unset (the default) means
    /// <see cref="TelemetryExtensions.ResolveServiceName"/> derives it from
    /// <see cref="System.Reflection.Assembly.GetEntryAssembly"/> — the actual running app,
    /// with zero maintenance if the project is ever renamed. Set this only when you need the
    /// collector to see a different logical name than the assembly's own (e.g. a blue-green or
    /// multi-tenant deployment sharing one binary under several names).
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// The reported <c>service.version</c>. Unset (the default) means
    /// <see cref="TelemetryExtensions.ResolveServiceVersion"/> derives it from the entry
    /// assembly's <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/> — your
    /// project's <c>&lt;Version&gt;</c>, plus a source-link commit hash on deterministic builds
    /// — genuinely useful for pinning a trace to the exact build that produced it (e.g. spotting
    /// a regression right after a deploy, or filtering by version during a canary rollout).
    /// </summary>
    public string? ServiceVersion { get; init; }
}
