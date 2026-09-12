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
}
