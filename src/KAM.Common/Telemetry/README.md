# Telemetry

OpenTelemetry: traces, metrics, and a Serilog log sink, all exported via OTLP to the same
collector. Config-driven like `Authorization/`/`Cors/` — nothing here is on by default because
it's hardcoded, it's on by default because it fails silently when nothing's listening.

## Files

| File | Purpose |
|---|---|
| `TelemetrySettings.cs` | `TelemetrySettings`, bound from the `Telemetry` section — `Enabled` (default `true`) and `OtlpEndpoint` (default unset, which means each exporter's own default: `http://localhost:4317`). |
| `TelemetryExtensions.cs` | `AddAppTelemetry(serviceName, settings)` — ASP.NET Core + `HttpClient` + .NET runtime instrumentation, traces and metrics, both exported via OTLP. A no-op when `Enabled` is `false`. |
| `ActivitySources.cs` | Shared `ActivitySource`s for custom spans below the auto-instrumented layer — currently `Pipeline`, used by `Pipeline<TContext>` (one span per step). |

Logging is deliberately **not** here: Serilog stays the app's `ILoggerFactory` everywhere else
(`CLAUDE.md`'s "`ILogger<T>` only" rule), so the OTLP log sink is one more `WriteTo.OpenTelemetry(...)`
line added directly to `Program.cs`'s existing `AddSerilog(...)` call — see its comment there.
Both that sink and `AddAppTelemetry` read the same `TelemetrySettings.OtlpEndpoint`, so one
config value drives all three signals to the same collector, and both need the same
`service.name` set explicitly (see "Why the log sink needs its own resource name" below).

## Why `serviceName` is a parameter, not a `KAM.Common` default

Same reasoning as `OpenApiExtensions.MapApiReference`'s `title` parameter: this library has no
identity of its own to report as *the* service. `Program.cs` passes its own name in.

## Why the log sink needs its own resource name

`AddAppTelemetry`'s `ConfigureResource(r => r.AddService(serviceName))` only shapes the
resource attached to *traces and metrics* — the OTel SDK's own pipeline. Serilog's OTLP sink is
a completely separate export path with its own resource attributes, so without also setting
`options.ResourceAttributes["service.name"]` in `Program.cs`'s `WriteTo.OpenTelemetry(...)`
call, logs show up in the collector labeled `unknown_service:<name>` while traces/metrics show
the clean name — a real, confirmed-live mismatch, not a hypothetical one.

## Why nothing here needs a null check for "is the collector up"

The OTLP exporter batches on a background thread; a failed export (connection refused) is
caught inside the exporter and reported to OpenTelemetry's own internal diagnostics, never
thrown into the request pipeline. There's no startup connectivity check either. Verified live:
the app starts and serves requests identically with the collector container up or fully
stopped — no exceptions, no visible log noise, just nothing to look at in the dashboard.

## Custom spans: the gotcha if you add another `ActivitySource`

`ActivitySource`'s constructor notifies every *already-registered* `ActivityListener`
synchronously, to let each one decide if it cares about the new source. If a listener's
`ShouldListenTo` predicate reads a source back off this class (`ActivitySources.Pipeline.Name`)
instead of a value captured beforehand, and that listener gets registered before this class has
been touched for the first time anywhere else, the predicate re-enters the static field while
it's still being assigned — sees `null`, throws — and because a failed static constructor is
cached for the process's lifetime, *every* later use of `ActivitySources` fails the same way,
not just the one caller that got the order wrong. See `ActivitySources.cs`'s own remarks and
`PipelineRegistrationTests.Each_step_runs_inside_its_own_span_when_something_is_listening` for
the concrete fix (capture the name to a local before calling `AddActivityListener`). Hit this
exactly once, writing that test.

## Local viewer

```bash
docker compose up -d      # .NET Aspire Dashboard — traces, logs, metrics, one UI, no signup
```

Dashboard at `http://localhost:18888`. The app's default `OtlpEndpoint` (unset →
`http://localhost:4317`) already points at the container's OTLP port — nothing to configure for
local dev. See the root `docker-compose.yml`.

## Usage

```csharp
// Program.cs
const string SERVICE_NAME = "AspNetDesignPatterns.Api";
TelemetrySettings telemetry = builder.Configuration.GetSection(TelemetrySettings.Section).Get<TelemetrySettings>() ?? new();

builder.Services.AddSerilog((services, configuration) =>
{
    configuration.ReadFrom.Configuration(builder.Configuration) /* … */;

    if (telemetry.Enabled)
    {
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.ResourceAttributes["service.name"] = SERVICE_NAME;
            if (telemetry.OtlpEndpoint is not null)
            {
                options.Endpoint = telemetry.OtlpEndpoint;
            }
        });
    }
});

builder.Services.AddAppTelemetry(SERVICE_NAME, telemetry);
```

A custom span anywhere else in the app follows the same shape as `Pipeline<TContext>`:

```csharp
using Activity? activity = ActivitySources.Pipeline.StartActivity("StepName");
// ... the operation ...
// disposed at scope exit; null (and free) when nothing's listening.
```

## Tests

`tests/KAM.Common.Tests/Telemetry/TelemetryExtensionsTests.cs` — `Enabled: true` registers a
`TracerProvider` and `MeterProvider`; `Enabled: false` registers neither.
`tests/KAM.Common.Tests/Pipeline/PipelineRegistrationTests.cs`'s
`Each_step_runs_inside_its_own_span_when_something_is_listening` proves the custom-span pattern
itself, via a real `ActivityListener`. End-to-end (an actual OTLP export received by a real
collector) was verified live against the Aspire Dashboard rather than as an automated test —
spinning up a container isn't a unit-test concern.
