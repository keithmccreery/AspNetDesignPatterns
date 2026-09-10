# DependencyInjection

The reflection-registration plumbing. One assembly scan per convention wires up everything
that follows a pattern; `IDependency` modules contribute anything that doesn't. **Adding a
feature never means editing this folder or `Program.cs`.**

The extension methods live in namespace `Microsoft.Extensions.DependencyInjection` (so they
surface on `IServiceCollection` without an extra `using`); `IDE0130` is suppressed on those
files on purpose.

## Files

| File | Type | Purpose |
|---|---|---|
| `IEndpoint.cs` | `IEndpoint` | `void MapEndpoint(IEndpointRouteBuilder app)`. One Minimal API endpoint, living in its feature slice. |
| `IDependency.cs` | `IDependency` | `void RegisterServices(IServiceCollection services)`. A feature's private DI that convention can't express — typed clients with resilience, OAuth handlers, caching, third-party SDKs. |
| `ISettings.cs` / `SettingsBase.cs` | `ISettings`, `SettingsBase<T>` | A settings class that **registers itself**: binds to the config section named by its own `public static string Section` and validates on startup. |
| `FluentValidateOptions.cs` | `IValidateOptions<TOptions>` | Adapter that runs a FluentValidation `IValidator<TOptions>` as options validation. Used by `SettingsBase` only when a validator is registered. |
| `GlobalExceptionHandler.cs` | `IExceptionHandler` | Last-resort: turns an uncaught exception into a 500 `ProblemDetails` via `IProblemDetailsService` (so it runs through the same `CustomizeProblemDetails` pipeline — same `traceId`/`instance` — as every other error), and logs it. |
| `DependencyInjectionExtensions.cs` | the scans | `AddEndpoints`, `MapEndpoints`, `AddSettings`, `AddDependencies`, `AddRequestHandlers`. |

## The scans (all in `Program.cs`, one line each)

| Call | Finds | Registers as |
|---|---|---|
| `AddEndpoints()` | `IEndpoint` | `IEndpoint` (transient, `TryAddEnumerable` — idempotent) |
| `AddSettings()` | `ISettings` | instantiates each, calls `RegisterSettings(services)` |
| `AddDependencies()` | `IDependency` | instantiates each, calls `RegisterServices(services)` |
| `AddRequestHandlers()` | closed `IRequestHandler<,>` impls | the closed interface → impl (scoped) |
| `AddPipeline()` / `AddPipelineSteps()` | (see [`../Shared/Pipeline`](../Shared/Pipeline/README.md)) | factory + steps (scoped) |
| `MapEndpoints(versionedGroup)` | resolves `IEndpoint`s | calls `MapEndpoint` on each, onto the versioned route group |

Order matters in `Program.cs`: `AddValidatorsFromAssembly(...)` **before** `AddSettings()`,
so `SettingsBase` can detect a registered `IValidator<T>` and pick the FluentValidation path
over DataAnnotations.

## `SettingsBase<T>`

```csharp
public sealed class WeatherOptions : SettingsBase<WeatherOptions>
{
    public static string Section => "Weather";        // required — RegisterSettings throws without it
    public string BaseAddress { get; init; } = "https://api.open-meteo.com";
    // …
}
```

- If an `IValidator<WeatherOptions>` is registered → validated with FluentValidation
  (per-rule failure messages surface as startup errors).
- Otherwise → `ValidateDataAnnotations()`.
- Always `ValidateOnStart()` — a bad `appsettings.json` fails the app immediately, not at
  first use.

## Adding a feature

1. Create `Features/<Name>/`.
2. Add a request + `AbstractValidator`, a response DTO, an `IRequestHandler`, an `IEndpoint`.
3. Feature-owned services (typed client, repository)? Add an `IDependency`.
4. Reads config? Add a `SettingsBase<T>` (+ a validator if you want FluentValidation).
5. Done — the scans pick all of it up.

## Tests

`tests/…/DependencyInjection/` — `DependencyInjectionExtensionsTests` (each scan, against
fixtures declared in the test assembly), `SettingsBaseTests` (both validation paths, missing
`Section` fails fast), `GlobalExceptionHandlerTests` (500 + `ProblemDetails`, declines
gracefully), `ReflectionRegistrationTests` (the scans against the real app).
