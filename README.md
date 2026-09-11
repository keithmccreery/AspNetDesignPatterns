# AspNetDesignPatterns

A reference ASP.NET Core **Minimal API** that demonstrates the patterns and standard
practices used on real .NET projects. It starts from the classic "weather" example and is
built to grow: features are vertical slices, cross-cutting concerns live in one place, and
new endpoints are discovered by reflection rather than wired by hand.

> Status: first cut. One feature slice (`Weather`) exercises every pattern end to end.

## Tech

| | |
|---|---|
| Runtime | .NET 10 (LTS) |
| Web | ASP.NET Core Minimal APIs, URL-segment API versioning (`Asp.Versioning`) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` (one doc per version) + [Scalar](https://scalar.com) UI |
| Logging | Serilog → `Microsoft.Extensions.Logging`, with CWE-117 log-injection hardening |
| Validation | FluentValidation (endpoint filter + options validation) |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly) — applied to every typed client |
| Auth | JWT bearer + authorization policies |
| Tests | NUnit + AwesomeAssertions + NSubstitute + `WebApplicationFactory` |

## Running it

```bash
# 1. Provide the dev JWT signing key
cp src/AspNetDesignPatterns.Api/.env.example src/AspNetDesignPatterns.Api/.env
#    edit .env and set Jwt__SigningKey to any string >= 32 chars

# 2. Run
dotnet run --project src/AspNetDesignPatterns.Api
```

Then:

- **Scalar UI**: <http://localhost:5139/scalar>
- **OpenAPI doc**: <http://localhost:5139/openapi/v1.json>
- **Health**: <http://localhost:5139/health/live> and <http://localhost:5139/health/ready>
- Get a token: `POST /api/v1/auth/token` with body `{ "subject": "me" }` (Development only)
- Call the feature: `GET /api/v1/weather/forecast?latitude=52.52&longitude=13.40&days=3`
  with header `Authorization: Bearer <token>`

`GET /api/v1/weather/forecast` proxies the public [open-meteo](https://open-meteo.com) API,
so it needs outbound network access; the tests do not.

## Testing

```bash
dotnet test
```

Unit tests cover the shared primitives (their own project,
`KAM.Common.Tests`) and the `Weather` handler/service/validator in
isolation. Integration tests (`tests/…/Integration`) boot the real app in memory with a
fake `IWeatherClient`, assert status codes and `ProblemDetails` bodies, and prove the two
projects' reflection scans actually discover each other's types (`ReflectionRegistrationTests`
— the two-assembly split's one real failure mode, see
[`KAM.Common/README.md`](src/KAM.Common/README.md)).
Architecture tests (`tests/…/Architecture`) enforce the conventions themselves — see its
README.

## Code quality

`dotnet build` runs the .NET analyzers plus Roslynator, Meziantou, StyleCop, AsyncFixer,
IDisposableAnalyzers, VS Threading and ErrorProne.NET. Compiler warnings are errors; the
third-party analyzer diagnostics are **advisory by default** (`dotnet_analyzer_diagnostic.severity
= suggestion` in `.editorconfig`) and promoted to `warning` per rule as they're adopted.

```bash
dotnet format --verify-no-changes          # style + whitespace, gated in CI
dotnet format analyzers --severity info     # see the advisory analyzer backlog
```

## Solution layout

Two projects on the `src/` side, split so the reusable half can never (even by accident)
reach into the app that hosts it — see
[`KAM.Common/README.md`](src/KAM.Common/README.md)
for why, and what that boundary actually cost when it was introduced.

```
Directory.Build.props / Directory.Packages.props   shared build settings + central package versions
src/AspNetDesignPatterns.Api/          the host — depends on the project below, never the other way
  Program.cs                 banner-organized composition root: .env, ProblemDetails, JSON,
                             Serilog, versioning + OpenAPI, auth, then the reflection scans
  GlobalUsings.cs
  Features/                  one folder per vertical slice (endpoint + handler + service + …)
    Weather/                 see Features/Weather/README.md
src/KAM.Common/             reusable, no reference to the project above — see its own README
  DependencyInjection/       reflection-registration conventions — see DependencyInjection/README.md
  Auth/ Authorization/ Cors/ Results/ Pipeline/ FluentValidations/ Logging/ OpenApi/
  Configuration/ Handlers/ HealthChecks/  cross-cutting building blocks, one top-level folder
                             per concern (each has its own README.md)
tests/AspNetDesignPatterns.Api.Tests/  tests AspNetDesignPatterns.Api (+ the two wired together)
  GlobalTestSetup.cs         [SetUpFixture] owning the one shared WebApplicationFactory
  TestSupport/               shared test doubles (currently just StubHttpMessageHandler)
  Features/ Integration/     — mirrors the src layout; Integration/ also proves the two
                             projects' reflection scans actually find each other's types
  Architecture/              NetArchTest rules that enforce the conventions — see its README
tests/KAM.Common.Tests/     tests KAM.Common in isolation — mirrors its layout exactly
  TestSupport/               shared test doubles (FakeHostEnvironment, ManageEnvironmentVariables)
```

Every shared service has a focused unit-test file mirroring its folder, and a `README.md`
next to the code. `src/KAM.Common/README.md` and `DependencyInjection/README.md` index them.

## Patterns — where to look

| Pattern | What it does | Start here |
|---|---|---|
| **Minimal APIs, vertical slices** | Each feature owns its endpoint, handler, DTOs, service and validator under `Features/<Name>/`. | [`Features/Weather/`](src/AspNetDesignPatterns.Api/Features/Weather) |
| **Reflection-based registration** | One assembly scan each for `IEndpoint`, `ISettings`, `IRequestHandler<,>`, `IPipelineStep<>`; `IDependency` modules contribute anything convention can't. No central file to edit per feature — every scan takes the assemblies explicitly, so it works across the `KAM.Common` / `AspNetDesignPatterns.Api` project boundary too. | [`DependencyInjection/`](src/KAM.Common/DependencyInjection) |
| **`IRequestHandler<TReq,TRes>`** | Endpoints are thin HTTP adapters; behaviour lives in a handler resolved from DI. | [`Handlers/IRequestHandler.cs`](src/KAM.Common/Handlers/IRequestHandler.cs), [`Features/Weather/GetForecastHandler.cs`](src/AspNetDesignPatterns.Api/Features/Weather/GetForecastHandler.cs) |
| **`Result` / `Result<T>`** | Every layer from Services up returns a result instead of throwing; only clients throw. `Error` carries a category, and optionally the originating exception + a logging context. | [`Results/Result.cs`](src/KAM.Common/Results/Result.cs), [`Error.cs`](src/KAM.Common/Results/Error.cs) |
| **Railway-oriented composition** | `Map` / `Bind` / `Ensure` / `Tap` chain fallible steps — sync and async (`…Async` overloads for `Task<Result>` inputs and async projections). `ResultUtilities` aggregates several results (`Combine`, `CombineAll`, `FirstSuccess`). | [`ResultExtensions.cs`](src/KAM.Common/Results/ResultExtensions.cs) + [`.Async.cs`](src/KAM.Common/Results/ResultExtensions.Async.cs), [`ResultUtilities.cs`](src/KAM.Common/Results/ResultUtilities.cs) |
| **Result logging** | `result.LogOnFailure(logger).ToHttpResult()` — one call logs the outcome with the error's exception + context; services don't take an `ILogger`. | [`Results/ResultLoggingExtensions.cs`](src/KAM.Common/Results/ResultLoggingExtensions.cs) |
| **`ProblemDetails` on errors** | One mapping from `Error` → status code → RFC 9457 body; endpoints just call `result.ToHttpResult()`. Same shape the global handler produces. | [`Results/ResultHttpExtensions.cs`](src/KAM.Common/Results/ResultHttpExtensions.cs) |
| **Global exception handler** | `IExceptionHandler` + `IProblemDetailsService` turns anything uncaught into a 500 `ProblemDetails` with a trace id. | [`DependencyInjection/ExceptionHandling/GlobalExceptionHandler.cs`](src/KAM.Common/DependencyInjection/ExceptionHandling/GlobalExceptionHandler.cs) |
| **Pipeline pattern** | Middleware-style: each `IPipelineStep<TContext>` gets a `next` delegate — it can short-circuit, or run code after the rest of the chain. Composed per use case via `IPipelineFactory`. | [`Pipeline/`](src/KAM.Common/Pipeline), [`Features/Weather/ForecastPipeline.cs`](src/AspNetDesignPatterns.Api/Features/Weather/ForecastPipeline.cs) |
| **FluentValidation** | An endpoint filter validates the request model at the HTTP edge; `ValidateAsResult` validates inside a service/pipeline step and returns a `Result`. | [`FluentValidations/ValidationFilter.cs`](src/KAM.Common/FluentValidations/ValidationFilter.cs), [`ValidationResultExtensions.cs`](src/KAM.Common/FluentValidations/ValidationResultExtensions.cs) |
| **Self-registering settings** | `SettingsBase<T>` binds a settings class to its own `Section` and validates on startup — FluentValidation if a validator is registered, else DataAnnotations. | [`DependencyInjection/Settings/SettingsBase.cs`](src/KAM.Common/DependencyInjection/Settings/SettingsBase.cs), [`WeatherOptions.cs`](src/AspNetDesignPatterns.Api/Features/Weather/WeatherOptions.cs) (FluentValidation), [`Auth/JwtOptions.cs`](src/KAM.Common/Auth/JwtOptions.cs) (DataAnnotations) |
| **Serilog → ILogger** | Config from `appsettings.json`; request logging; the app only ever depends on `ILogger<T>`. | `Program.cs` (`AddSerilog`) |
| **Log-injection hardening (CWE-117)** | A Serilog enricher strips CR/LF from every scalar log property, globally. | [`Logging/ControlCharacterSanitizingEnricher.cs`](src/KAM.Common/Logging/ControlCharacterSanitizingEnricher.cs), [`LogSanitizer.cs`](src/KAM.Common/Logging/LogSanitizer.cs) |
| **OpenAPI + Scalar + versioning** | `AddApiVersioning().AddOpenApi()` produces one document per API version; a transformer adds the JWT scheme; the document is served outside Production and Scalar renders it in Development — both anonymous. | `Program.cs`, [`OpenApi/OpenApiExtensions.cs`](src/KAM.Common/OpenApi/OpenApiExtensions.cs) |
| **System.Text.Json** | One `JsonSerializerOptions` built in `Program.cs`, registered as a singleton, and mirrored into Minimal API serialization. | `Program.cs` (`CreateJsonSerializerOptions`) |
| **HttpClient + Polly** | `ConfigureHttpClientDefaults(... AddStandardResilienceHandler())` gives every typed client retry / circuit-breaker / timeout. | `Program.cs`, [`Features/Weather/WeatherDependencies.cs`](src/AspNetDesignPatterns.Api/Features/Weather/WeatherDependencies.cs) |
| **`.env` files** | `DotNetEnv` loads `.env` into environment variables before configuration is built. | `Program.cs`, [`.env.example`](src/AspNetDesignPatterns.Api/.env.example) |
| **Environment model** | `AppEnvironment` — a small "where/how am I running" view (env name, containerized) used for Kestrel and Scalar gating. | [`Configuration/AppEnvironment.cs`](src/KAM.Common/Configuration/AppEnvironment.cs) |
| **AuthN (JWT bearer)** | Bearer token validation configured from `JwtOptions` (issuer/audience/lifetime toggles, clock skew, claim-type overrides), `JwtBearerEvents` logging, a Development-only dev-token endpoint (with its own request validator). | [`Auth/`](src/KAM.Common/Auth) |
| **AuthZ (config-driven policies)** | Named authorization policies — required scopes/roles/claims, plus custom requirements — defined in `appsettings.json`, not hardcoded C#; scopes checked via a space-delimited-claim-aware helper so it works against a real IdP, not just this app's own tokens; a **deny-by-default fallback policy**; a worked custom `IAuthorizationRequirement` example (`ValidClientIdRequirement`). | [`Authorization/`](src/KAM.Common/Authorization) |
| **CORS** | A single named policy configured from `appsettings.json`; `"*"` means "any" for headers/methods (not a literal value), and a `"*"` origin can't combine with credentials — both enforced at startup, not as a runtime browser surprise. | [`Cors/`](src/KAM.Common/Cors) |
| **Health checks** | `/health/live` (no checks) and `/health/ready` (checks tagged `ready`), split by intent, with a small custom JSON writer that hides descriptions/exception messages in Production. Checks self-register from the owning slice and cache anything that calls out over the network, since the routes are anonymous. | [`HealthChecks/`](src/KAM.Common/HealthChecks), [`Features/Weather/WeatherProviderHealthCheck.cs`](src/AspNetDesignPatterns.Api/Features/Weather/WeatherProviderHealthCheck.cs) |
| **DI graph validation** | `UseDefaultServiceProvider(ValidateOnBuild = true, ValidateScopes = true)` — startup fails on a mis-wired or captive dependency. | `Program.cs` |
| **Architecture tests** | The conventions enforced as tests: services return `Result`, only clients throw, no cross-feature dependencies, role types named + sealed (NetArchTest, static analysis) — plus every mapped endpoint declares an explicit auth intent, and the OpenAPI/Scalar exposure gating holds (built against a real host, since that's invisible to static analysis). | [`tests/…/Architecture/`](tests/AspNetDesignPatterns.Api.Tests/Architecture) |

For a narrated walk-through of how a single request touches all of the above, read
**[`Features/Weather/README.md`](src/AspNetDesignPatterns.Api/Features/Weather/README.md)** —
it is also the template for adding a new feature slice.

## License

[MIT](LICENSE)
