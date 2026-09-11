# HealthChecks

The two probes an orchestrator expects, split by intent, rendered as small readable JSON.

| Route | Runs | Purpose |
|---|---|---|
| `GET /health/live` | **nothing** | "Is the process wedged?" A response at all ⇒ don't restart the pod. Dependency health is deliberately *not* checked here — a downstream outage must not trigger a restart loop. |
| `GET /health/ready` | every check tagged `ready` | "Should this instance receive traffic?" `Healthy`/`Degraded` ⇒ 200, `Unhealthy` ⇒ 503 (orchestrator pulls it from the load balancer). |

Both are **anonymous**, both are **hidden from the OpenAPI document**, both sit at the root
(not under `/api/v{version}`).

## Files

| File | Type | Purpose |
|---|---|---|
| `HealthCheckTag.cs` | `static class` | `HealthCheckTag.Ready` — the tag `/health/ready` filters on. No `Live` equivalent: liveness runs zero checks, so nothing would ever read it. |
| `HealthCheckResponseWriter.cs` | `static` writer | Serializes a `HealthReport` to `{ status, totalDurationMs, entries: { name: { status, durationMs, description, error, tags } } }` using the app's shared `JsonSerializerOptions`. `description`/`error` (exception **message**, never a stack trace) are included only outside Production — both routes are anonymous, so a hand-written check `Description` or an `Exception.Message` (hostname, port, connection-string fragment) must not reach an unauthenticated caller in Production. |
| `HealthCheckExtensions.cs` | `MapAppHealthChecks()` | Maps the two routes with the tag predicates and the response writer. |

## Wiring

```csharp
// Program.cs — services (framework API, like AddHttpContextAccessor)
builder.Services.AddHealthChecks();

// Program.cs — endpoints
app.MapAppHealthChecks();
```

Individual checks self-register from the slice that owns them — they are **not** listed in a
central file:

```csharp
// Features/Weather/WeatherDependencies.cs
services.AddHealthChecks().AddCheck<WeatherProviderHealthCheck>(
    "weather-provider",
    failureStatus: HealthStatus.Degraded,   // provider down ≠ instance not ready
    tags: [HealthCheckTag.Ready]);
```

## Conventions

- **Readiness ≠ "everything is perfect".** A dependency the app degrades around (the weather
  endpoint returns a 502 `ProblemDetails`, the rest keeps working) reports `Degraded`, not
  `Unhealthy` — the instance stays in rotation and the report still shows the problem. Use
  `Unhealthy` only when the instance genuinely cannot serve.
- **A probe is one fast request.** `WeatherProviderHealthCheck` uses its own `HttpClient`
  with `RemoveAllResilienceHandlers()` and a short timeout, so a probe is not a retrying
  Polly pipeline.
- **An anonymous readiness route must not be a free amplifier.** `/health/ready` takes no
  credentials, so without a cache every request to it would drive one outbound call to
  open-meteo. `WeatherProviderHealthCheck` caches its result for 15s via `IMemoryCache`
  (`builder.Services.AddMemoryCache()` in `Program.cs`) — cheap insurance, and readiness
  doesn't need fresher-than-a-few-seconds data anyway. A check with no external call (most of
  them) doesn't need this.
- **No third-party UI package.** `HealthCheckResponseWriter` is ~40 lines and gives full
  control of the shape; `AspNetCore.HealthChecks.UI.Client` would be the drop-in alternative
  if a dashboard needs its exact schema.

## Tests

- `tests/KAM.Common.Tests/HealthChecks/HealthCheckResponseWriterTests.cs` — the JSON shape from a
  synthetic `HealthReport`, and that `description`/`error` are present outside Production and
  omitted in Production.
- `tests/…/Features/Weather/WeatherProviderHealthCheckTests.cs` — Healthy on 2xx, Degraded on
  an error status or an unreachable host (fake `HttpMessageHandler`), and that a second probe
  within the cache window doesn't call the provider again.
- `tests/…/Integration/HealthCheckEndpointsTests.cs` — the real app: `/health/live` runs no
  checks, `/health/ready` runs the `ready`-tagged checks, neither appears in the OpenAPI doc.
  The integration factory stubs the weather probe's `HttpClient` so readiness is deterministic.
