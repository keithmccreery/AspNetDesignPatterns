# Telemetry

OpenTelemetry: traces, metrics, and a Serilog log sink, all exported via OTLP to the same
collector. Config-driven like `Authorization/`/`Cors/` — nothing here is on by default because
it's hardcoded, it's on by default because it fails silently when nothing's listening.

## Files

| File | Purpose |
|---|---|
| `TelemetrySettings.cs` | `TelemetrySettings`, bound from the `Telemetry` section — `Enabled` (default `true`), `OtlpEndpoint` (default unset → each exporter's own default, `http://localhost:4317`), `ServiceName`/`ServiceVersion` (default unset → derived from the entry assembly, see below). |
| `TelemetryExtensions.cs` | `AddAppTelemetry(settings)` — ASP.NET Core + `HttpClient` + .NET runtime instrumentation, traces and metrics, both exported via OTLP. A no-op when `Enabled` is `false`. Also `ResolveServiceName`/`ResolveServiceVersion`, shared with the Serilog sink in `Program.cs`. |
| `ActivitySources.cs` | Shared `ActivitySource`s for custom spans below the auto-instrumented layer — currently `Pipeline`, used by `Pipeline<TContext>` (one span per step). |

Logging is deliberately **not** here: Serilog stays the app's `ILoggerFactory` everywhere else
(`CLAUDE.md`'s "`ILogger<T>` only" rule), so the OTLP log sink is one more `WriteTo.OpenTelemetry(...)`
line added directly to `Program.cs`'s existing `AddSerilog(...)` call — see its comment there.
Both that sink and `AddAppTelemetry` read the same `TelemetrySettings.OtlpEndpoint`, and both
call `ResolveServiceName`/`ResolveServiceVersion`, so one config section drives all three
signals to the same collector under the same identity.

## Why `ServiceName`/`ServiceVersion` are resolved, not required

`KAM.Common` still has no identity of its own to *own* as a default — same reasoning as
`OpenApiExtensions.MapApiReference`'s `title` parameter — but unlike a UI title, a service's
OTel identity has an obvious, zero-maintenance default: whichever assembly is actually running.
`ResolveServiceName` returns `TelemetrySettings.ServiceName` if set, otherwise
`Assembly.GetEntryAssembly()`'s own name; `ResolveServiceVersion` does the same with
`AssemblyInformationalVersionAttribute` (your project's `<Version>`, plus a source-link commit
hash on deterministic builds — genuinely useful for pinning a trace to the exact build that
produced it, e.g. spotting a regression right after a deploy or filtering by version during a
canary rollout). The config override exists for the real case that isn't "the library making
something up" — a blue-green or multi-tenant deployment that wants the *same binary* to report
under a different logical name without a recompile.

## `service.name` (the Resource) vs. an `ActivitySource`'s name (the Instrumentation Scope)

`ActivitySources.Pipeline`'s name, `"KAM.Common.Pipeline"`, is **not** app identity and is
**never** overwritten with the resolved service name — it answers a different question. Every
exported span carries both, together, and they mean different things:

- **`service.name`** — *which running process* produced this signal. Set once, for the whole
  app, in `ConfigureResource(...)`.
- **the `ActivitySource`'s name** — *which library/component* emitted this specific span,
  independent of who's consuming it. This is the OTel equivalent of an `ILogger<T>` category —
  nobody would suggest `ILogger<Pipeline<T>>`'s category should get overwritten with the app's
  own name either. `"KAM.Common.Pipeline"` should read `"KAM.Common.Pipeline"` in *any* app that
  uses this library, so someone querying a collector can ask "show me every span the Pipeline
  abstraction produced" across apps, while `service.name` separately answers which app that was
  in. ASP.NET Core follows the identical convention: its own instrumentation reports as
  `"Microsoft.AspNetCore.Hosting"` regardless of which app hosts it.

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
TelemetrySettings telemetry = builder.Configuration.GetSection(TelemetrySettings.Section).Get<TelemetrySettings>() ?? new();
string telemetryServiceName = TelemetryExtensions.ResolveServiceName(telemetry);
string? telemetryServiceVersion = TelemetryExtensions.ResolveServiceVersion(telemetry);

builder.Services.AddSerilog((services, configuration) =>
{
    configuration.ReadFrom.Configuration(builder.Configuration) /* … */;

    if (telemetry.Enabled)
    {
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.ResourceAttributes["service.name"] = telemetryServiceName;
            if (telemetryServiceVersion is not null)
            {
                options.ResourceAttributes["service.version"] = telemetryServiceVersion;
            }

            if (telemetry.OtlpEndpoint is not null)
            {
                options.Endpoint = telemetry.OtlpEndpoint;
            }
        });
    }
});

builder.Services.AddAppTelemetry(telemetry);
```

### Custom spans

The plain shape — same as `Pipeline<TContext>` uses for every step:

```csharp
using Activity? activity = ActivitySources.Pipeline.StartActivity("FetchForecastStep");
// ... the operation ...
// disposed at scope exit; null (and free) when nothing's listening.
```

### Enriching a span with tags

Tags describe the specific operation a span represents — attach them at creation with the
built-in `ActivitySource.StartActivity(ActivityKind, ...)` overload (named arguments, no new
API to learn) rather than a series of null-conditional `SetTag` calls afterward:

```csharp
using Activity? activity = ActivitySources.Pipeline.StartActivity(
    ActivityKind.Internal,
    name: "FetchForecastStep",
    tags:
    [
        new("weather.latitude", context.Request.Latitude),
        new("weather.longitude", context.Request.Longitude),
        new("weather.days", context.Request.Days),
    ]);
```

Tags computed *during* the operation — not known until partway through — still use `SetTag`,
just on the activity this same call already started:

```csharp
using Activity? activity = ActivitySources.Pipeline.StartActivity("RoundTemperaturesStep");
List<DailyForecast> rounded = /* ... */;
activity?.SetTag("weather.days_rounded", rounded.Count);
```

### Recording a discrete event inside a span

Use `AddEvent` for something that happens *at a point in time* during the operation, rather
than a property of the whole span — e.g. a cache decision:

```csharp
using Activity? activity = ActivitySources.Pipeline.StartActivity("ClampForecastWindowStep");
if (context.Request.Days > options.Value.MaxForecastDays)
{
    activity?.AddEvent(new ActivityEvent(
        "forecast-window-clamped",
        tags: [new("weather.requested_days", context.Request.Days), new("weather.max_days", options.Value.MaxForecastDays)]));
}
```

This is also how you'd model a "custom business event" (`OrderPlaced`, `TokenIssued`, …) inside
an existing span — OTel's data model doesn't have a separate "events" API distinct from spans
and logs; a span event (scoped to one trace) or a structured `ILogger` call (standalone,
already this repo's convention) are the two ways to express one.

## Tests

`tests/KAM.Common.Tests/Telemetry/TelemetryExtensionsTests.cs` — `Enabled: true` registers a
`TracerProvider` and `MeterProvider`, `Enabled: false` registers neither;
`ResolveServiceName`/`ResolveServiceVersion` prefer the configured value over the assembly-derived
fallback. `tests/KAM.Common.Tests/Pipeline/PipelineRegistrationTests.cs`'s
`Each_step_runs_inside_its_own_span_when_something_is_listening` proves the custom-span pattern
itself, via a real `ActivityListener`. End-to-end (an actual OTLP export received by a real
collector) was verified live against the Aspire Dashboard rather than as an automated test —
spinning up a container isn't a unit-test concern.
