# Feature slice: Weather

`GET /api/v1/weather/forecast?latitude={lat}&longitude={lon}&days={n}` — a daily forecast
for a set of coordinates, proxied from the public [open-meteo](https://open-meteo.com) API.

This slice is the reference implementation. Every pattern in the repo shows up here once;
copy this folder's shape when adding a feature.

## Files

| File | Role |
|---|---|
| `GetForecastEndpoint.cs` | `IEndpoint` — maps the route, wires auth + validation, then `handler.HandleAsync(…).LogOnFailureAsync(logger).ToHttpResultAsync()` (async `Result` chain). |
| `GetForecastRequest.cs` | Request model (`[AsParameters]`) **and** its `AbstractValidator`. |
| `ForecastResponse.cs` | The API's response contract — independent of the upstream shape. |
| `GetForecastHandler.cs` | `IRequestHandler` — composes and runs the pipeline via `IPipelineFactory`. |
| `ForecastPipeline.cs` | `ForecastContext` + the middleware steps (clamp window → fetch → round). |
| `WeatherService.cs` | First layer above the client: turns values / nulls / exceptions into `Result<T>`; maps upstream → contract. |
| `IWeatherClient.cs` | Client abstraction + `WeatherClientException`. The only layer allowed to throw. |
| `OpenMeteoWeatherClient.cs` | Typed `HttpClient` implementation (resilience is applied globally). Builds the query with `QueryHelpers.AddQueryString` (URL-encoded, culture-invariant) and validates the daily-forecast shape before returning it, so a malformed-but-parseable payload becomes a `WeatherClientException`, not an unhandled exception three layers up. |
| `OpenMeteoForecast.cs` | The upstream JSON shape (client boundary only). |
| `WeatherOptions.cs` | `SettingsBase<WeatherOptions>` — binds the `Weather` section + FluentValidation validator. |
| `WeatherProviderHealthCheck.cs` | `IHealthCheck` — readiness probe for open-meteo. Reports `Degraded` (not `Unhealthy`) when it's down, since the endpoint degrades to a 502. |
| `WeatherDependencies.cs` | `IDependency` — registers `WeatherService`, the typed client, the health-check client (resilience removed), and the `ready`-tagged check. |

## Request flow

```
HTTP GET /api/v1/weather/forecast
  │
  ▼
GetForecastEndpoint ──────────────── RequireAuthorization("weather:read")   ← Shared/Auth
  │                                   ValidationFilter<GetForecastRequest>  ← Shared/FluentValidations
  │                                     └─ invalid → 400 ProblemDetails (never reaches the handler)
  ▼
IRequestHandler<GetForecastRequest, ForecastResponse>  =  GetForecastHandler
  │   builds:  factory.CreateBuilder<ForecastContext>()
  │              .Use<ClampForecastWindowStep>()
  │              .Use<FetchForecastStep>()
  │              .Use<RoundTemperaturesStep>()
  ▼
Pipeline<ForecastContext>.ExecuteAsync   ← Shared/Pipeline  (middleware: each step gets `next`)
  ├─ ClampForecastWindowStep   → over the configured max? return Result.Failure(Validation), skip `next`
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
  ├─ NotFound       → 404 ProblemDetails
  └─ Upstream       → 502 ProblemDetails
```

Anything thrown and not caught (a bug, not an expected failure) is caught by
`GlobalExceptionHandler` and returned as a 500 `ProblemDetails`.

## Configuration

```jsonc
// appsettings.json
"Weather": {
  "BaseAddress": "https://api.open-meteo.com",
  "RequestTimeout": "00:00:10",
  "MaxForecastDays": 16
}
```

Validated on startup by `WeatherOptionsValidator` (FluentValidation, because a validator is
registered). Set `MaxForecastDays` to `999` and the app fails fast with a clear message —
that is the settings-validation pattern working.

## Tests

- `tests/…/Features/Weather/` — validator, service (mapping + error translation) and
  handler (pipeline behaviour) in isolation with a substitute `IWeatherClient`.
- `tests/…/Features/Weather/OpenMeteoWeatherClientTests.cs` — the real client against a fake
  `HttpMessageHandler`: culture-invariant/URL-encoded query, 400→null, 5xx/transport/JSON
  failures and a malformed daily-forecast shape → `WeatherClientException`, and caller
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
