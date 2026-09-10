# Shared/FluentValidations

Request validation with [FluentValidation](https://docs.fluentvalidation.net), at two seams:
the **HTTP edge** (an endpoint filter) and **inside a service or pipeline step** (a `Result`).

Validators themselves live next to the request model they validate, in the feature slice —
e.g. `Features/Weather/GetForecastRequest.cs`. They are reflection-registered at startup by
`AddValidatorsFromAssembly(...)` in `Program.cs`.

## Files

| File | Type | Purpose |
|---|---|---|
| `ValidationFilter.cs` | `ValidationFilter<TRequest>` : `IEndpointFilter` | Resolves `IValidator<TRequest>` from DI and runs it before the route handler. On failure, short-circuits to a 400 `ValidationProblem` (per-field errors) — the handler never sees an invalid request. Passes through when no validator is registered or the argument is absent. |
| `ValidationEndpointExtensions.cs` | `ValidateRequest<TRequest>()` | `RouteHandlerBuilder` extension: adds the filter **and** `.ProducesValidationProblem()` so the 400 shows in OpenAPI. |
| `ValidationResultExtensions.cs` | `ValidationResult.ToResult()`, `IValidator<T>.ValidateAsResult[Async]()` | Bridges FluentValidation to the `Result` pattern for use *inside* a service/pipeline. A rejection becomes a `ValidationError`, so it still renders as an RFC 9457 validation problem if it reaches `ToHttpResult()`. |

## Usage

```csharp
// HTTP edge — in the endpoint mapping
app.MapGet("/weather/forecast", Handler)
   .ValidateRequest<GetForecastRequest>();   // filter + OpenAPI 400

// Inside a service / pipeline step
var validation = _validator.ValidateAsResult(command);
if (validation.IsFailure) return validation;   // ValidationError -> 400 ProblemDetails downstream
```

## Which seam to use

- **Endpoint filter** — the default. DRY, impossible to forget, keeps the handler pure.
- **`ValidateAsResult`** — when validation must happen at a *specific point* in a multi-step
  flow (after enrichment, before a second pass), or against a value that isn't the raw
  request. A filter can't express that.

## Tests

`tests/…/Shared/FluentValidations/` — `ValidationFilterTests` (valid → handler runs; invalid
→ 400 `ValidationProblem`; pass-through cases), `ValidationResultExtensionsTests` (sync +
async, per-field structure preserved). `ValidateRequest<T>` end-to-end is covered by
`tests/…/Integration/WeatherEndpointTests` (the 400 case).
