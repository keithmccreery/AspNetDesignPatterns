# Feature slice: Weather

`GET /api/v1/weather/forecast?latitude={lat}&longitude={lon}&days={n}` — a daily forecast
for a set of coordinates, proxied from the public [open-meteo](https://open-meteo.com) API.

This slice is the reference implementation. Every pattern in the repo shows up here once;
copy this folder's shape when adding a feature.

## Files

| File | Role |
|---|---|
| `GetForecastEndpoint.cs` | `IEndpoint` — maps the route, wires auth + validation, then `handler.HandleAsync(…).LogOnFailureAsync(logger).ToHttpResultAsync()` (async `Result` chain). |
| `GetForecastRequest.cs` | Request model (`[AsParameters]`) **and** its `AbstractValidator` — the one owner of every request rule, including the forecast window (`WeatherOptions.MaxForecastDays`, injected). |
| `ForecastResponse.cs` | The API's response contract — independent of the upstream shape. |
| `GetForecastHandler.cs` | `IRequestHandler` — composes and runs the pipeline via `IPipelineFactory`. |
| `ForecastPipeline.cs` | `ForecastContext` + the middleware steps (fetch → round). |
| `WeatherService.cs` | First layer above the client: turns values / exceptions into `Result<T>`; maps upstream → contract. |
| `IWeatherClient.cs` | Client abstraction + `WeatherClientException`. The only layer allowed to throw. |
| `OpenMeteoWeatherClient.cs` | Typed `HttpClient` implementation (resilience is applied globally). Builds the query with `QueryHelpers.AddQueryString` (URL-encoded, culture-invariant) and validates the daily-forecast shape before returning it, so a malformed-but-parseable payload becomes a `WeatherClientException`, not an unhandled exception three layers up. |
| `OpenMeteoForecast.cs` | The upstream JSON shape (client boundary only). |
| `WeatherOptions.cs` | `SettingsBase<WeatherOptions>` — binds the `Weather` section + FluentValidation validator. |
| `WeatherProviderHealthCheck.cs` | `IHealthCheck` — readiness probe for open-meteo. Reports `Degraded` (not `Unhealthy`) when it's down, since the endpoint degrades to a 502. |
| `WeatherAuthorization.cs` | `WeatherPolicies.Read` (the policy *name* the endpoint requires) and `WeatherScopes.Read` (the scope *value* its config checks) — the slice owns its authorization vocabulary, not `KAM.Common`. |
| `WeatherDependencies.cs` | `IDependency` — registers `WeatherService`, the typed client, the health-check client (resilience removed), and the `ready`-tagged check. |

## Request flow

```
HTTP GET /api/v1/weather/forecast
  │
  ▼
GetForecastEndpoint ──────────────── RequireAuthorization(WeatherPolicies.Read)   ← Shared/Auth
  │                                   ValidationFilter<GetForecastRequest>  ← Shared/FluentValidations
  │                                     └─ invalid → 400 ProblemDetails (never reaches the handler)
  ▼
IRequestHandler<GetForecastRequest, ForecastResponse>  =  GetForecastHandler
  │   builds:  factory.CreateBuilder<ForecastContext>()
  │              .Use<FetchForecastStep>()
  │              .Use<RoundTemperaturesStep>()
  ▼
Pipeline<ForecastContext>.ExecuteAsync   ← Shared/Pipeline  (middleware: each step gets `next`)
  ├─ FetchForecastStep         → calls WeatherService; on success sets context.Response, calls `next`
  │      │
  │      ▼
  │   WeatherService  ──────────────────── returns Result<ForecastResponse>  (takes no ILogger)
  │      │                                 catches WeatherClientException →
  │      │                                 Error.Upstream(...).WithException(ex).WithContext(coords)
  │      ▼
  │   IWeatherClient  =  OpenMeteoWeatherClient   ← typed HttpClient + Polly (global default)
  │      │                                          may throw WeatherClientException
  │      ▼
  │   api.open-meteo.com
  │
  └─ RoundTemperaturesStep     → runs AFTER `next`: rounds context.Response temperatures
  │
  ▼
Task<Result<ForecastResponse>>
  │
  ▼
.LogOnFailureAsync(logger)     ← one place logs the outcome (exception + context come from the Error)
  │
  ▼
.ToHttpResultAsync()           ← Shared/Results
  ├─ success        → 200 ForecastResponse
  ├─ Validation     → 400 ValidationProblemDetails
  └─ Upstream       → 502 ProblemDetails
```

Anything thrown and not caught (a bug, not an expected failure) is caught by
`GlobalExceptionHandler` and returned as a 500 `ProblemDetails`.

## Configuration

```jsonc
// appsettings.json
"Weather": {
  "BaseAddress": "https://api.open-meteo.com",
  "AttemptTimeout": "00:00:10",      // Polly per-attempt timeout (retried)
  "TotalRequestTimeout": "00:00:30", // Polly total timeout, retries included
  "MaxForecastDays": 16
}
```

Validated on startup by `WeatherOptionsValidator` (FluentValidation, because a validator is
registered). `HttpClient.Timeout` is deliberately infinite: it wraps the whole handler chain,
retries included, so a finite value races Polly — Polly's two timeouts above are the only
clock. Set `MaxForecastDays` to `999` and the app fails fast with a clear message —
that is the settings-validation pattern working. Set it to `7` and requests for more than 7
days get a 400 from `GetForecastRequestValidator`, which reads the same value — the request
rule and the config can't drift apart.

## Tests

- `tests/…/Features/Weather/` — validator, service (mapping + error translation) and
  handler (pipeline behaviour) in isolation with a substitute `IWeatherClient`.
- `tests/…/Features/Weather/OpenMeteoWeatherClientTests.cs` — the real client against a fake
  `HttpMessageHandler`: culture-invariant/URL-encoded query, any non-2xx (400 included),
  transport/JSON failures, resilience-handler rejections (circuit open, Polly timeout) and a
  missing or malformed daily-forecast shape → `WeatherClientException`, and caller
  cancellation propagating **unwrapped** (only a provider-side timeout should become a
  `WeatherClientException` — both look like `TaskCanceledException` to `HttpClient`).
- `tests/…/Integration/WeatherEndpointTests.cs` — the real app in memory: 401 without a
  token, 200 with one, 400 on a bad parameter, 502 when the client throws.
- `tests/…/Features/Weather/WeatherProviderHealthCheckTests.cs` — the readiness check:
  Healthy on 2xx, Degraded on an error status or an unreachable host.

## Adding a new feature slice

1. Create `Features/<Name>/`.
2. Add a request + `AbstractValidator`, a response DTO, an `IRequestHandler`, and an
   `IEndpoint`.
3. If the feature needs its own services (a typed client, a repository), add an
   `IDependency`.
4. If it reads configuration, add a `SettingsBase<T>` class (and a validator if you want
   FluentValidation rather than DataAnnotations).
5. That's it — reflection registers all of the above; no central file to edit.

### Not every slice needs every layer

This slice is deliberately maximal — it exists to show every pattern once, and the repo will
keep adding more. A real slice should take only the layers it needs:

- **No pipeline** unless there are genuinely composable steps (gates, enrichment,
  post-processing) — a handler can call the service directly and return its `Result<T>`.
- **No separate service** when there's no client exception to translate or upstream shape to
  map — the handler can own that logic.
- The endpoint → handler → `Result` → `ToHttpResult()` spine and the "only clients throw"
  rule are the parts that *aren't* optional.

