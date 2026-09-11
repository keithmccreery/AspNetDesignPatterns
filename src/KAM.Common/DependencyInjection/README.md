# DependencyInjection

The reflection-registration plumbing. One assembly scan per convention wires up everything
that follows a pattern; `IDependency` modules contribute anything that doesn't. **Adding a
feature never means editing this folder or `Program.cs`.**

One folder per concern (mirroring `../Pipeline/`'s shape — one cohesive folder per concern,
not a flat bucket): each pairs the contract with the registration extension that scans for
it, and — where the contract has a shape of its own to explain — its `SettingsBase<T>` /
`FluentValidateOptions<T>` neighbors.

| Folder | Contract | Registration extension |
|---|---|---|
| `Endpoints/` | `IEndpoint` — `void MapEndpoint(IEndpointRouteBuilder app)` | `EndpointRegistration`: `AddEndpoints()`, `MapEndpoints()` |
| `Settings/` | `ISettings`, `SettingsBase<T>`, `FluentValidateOptions<T>` | `SettingsRegistration`: `AddSettings()` |
| `Dependencies/` | `IDependency` — `void RegisterServices(IServiceCollection services)` | `DependencyRegistration`: `AddDependencies()` |
| `RequestHandlers/` | (contract is `IRequestHandler<,>`, in `../Handlers/`) | `RequestHandlerRegistration`: `AddRequestHandlers()` |
| `ExceptionHandling/` | `GlobalExceptionHandler` (`IExceptionHandler`) | `ExceptionHandlingRegistration`: `AddGlobalExceptionHandler()` |

Plus `AssemblyTypeScanning.cs` at the top level — the one "find concrete types in these
assemblies" step all five registration extensions above (and `../Pipeline/`'s
`AddPipelineSteps`) share, so that filter is written and reasoned about once.

The registration extensions live in namespace `Microsoft.Extensions.DependencyInjection` (so
they surface on `IServiceCollection` without an extra `using`); `IDE0130` is suppressed on
those files on purpose. The contracts themselves (`IEndpoint`, `ISettings`, `IDependency`,
`SettingsBase<T>`, …) stay in `KAM.Common.DependencyInjection` regardless of
which subfolder they live in — the subfolder is filing, not a namespace boundary.

## The scans (all in `Program.cs`, one line each)

| Call | Finds | Registers as |
|---|---|---|
| `AddEndpoints(assemblies)` | `IEndpoint` | `IEndpoint` (transient, `TryAddEnumerable` — idempotent) |
| `AddSettings(assemblies)` | `ISettings` | instantiates each, calls `RegisterSettings(services)` |
| `AddDependencies(assemblies)` | `IDependency` | instantiates each, calls `RegisterServices(services)` |
| `AddRequestHandlers(assemblies)` | closed `IRequestHandler<,>` impls | the closed interface → impl (scoped) |
| `AddGlobalExceptionHandler()` | — | `GlobalExceptionHandler` as the app's `IExceptionHandler` |
| `AddPipeline()` / `AddPipelineSteps(assemblies)` | (see [`../Pipeline`](../Pipeline/README.md)) | factory + steps (scoped) |
| `MapEndpoints(versionedGroup)` | resolves `IEndpoint`s | calls `MapEndpoint` on each, onto the versioned route group |

**Every scan takes an explicit `params Assembly[]`, and `Program.cs` always passes both**
`typeof(Program).Assembly` and `typeof(IEndpoint).Assembly` (this project's) **— never the
zero-argument default.** The default (`Assembly.GetCallingAssembly()`) exists for a
single-assembly app; this reference app is two assemblies (this library plus the host), and a
type living in either one needs to be found. Missing this is exactly how `JwtOptions` and
`DevTokenEndpoint` went briefly undiscovered when this folder moved into its own project — see
[`../README.md`](../README.md) for the story. If a third project is ever added that can
define an endpoint/setting/dependency/handler/step, extend the array in `Program.cs`.

Order also still matters: `AddValidatorsFromAssemblies(...)` **before** `AddSettings(...)`, so
`SettingsBase` can detect a registered `IValidator<T>` and pick the FluentValidation path over
DataAnnotations.

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

1. Create `Features/<Name>/` (in `AspNetDesignPatterns.Api`, not `KAM.Common`).
2. Add a request + `AbstractValidator`, a response DTO, an `IRequestHandler`, an `IEndpoint`.
3. Feature-owned services (typed client, repository)? Add an `IDependency`.
4. Reads config? Add a `SettingsBase<T>` (+ a validator if you want FluentValidation).
5. Done — the scans pick all of it up, from either assembly.

## Tests

`tests/KAM.Common.Tests/DependencyInjection/` mirrors this folder's
shape — `Endpoints/EndpointRegistrationTests`, `Dependencies/DependencyRegistrationTests`,
`RequestHandlers/RequestHandlerRegistrationTests` (each scan, against fixtures declared in the
test assembly), `Settings/SettingsBaseTests` (both validation paths, missing `Section` fails
fast), `ExceptionHandling/GlobalExceptionHandlerTests` (500 + `ProblemDetails`, declines
gracefully). The scans *actually finding types across both assemblies* — the one thing a
KAM.Common-only test can't prove — is `ReflectionRegistrationTests` in
`tests/AspNetDesignPatterns.Api.Tests/Integration/`.
