# DependencyInjection

The reflection-registration plumbing. One assembly scan per convention wires up everything
that follows a pattern; `IDependency` modules contribute anything that doesn't. **Adding a
feature never means editing this folder or `Program.cs`.**

One folder per concern (mirroring `../Pipeline/`'s shape — one cohesive folder per concern,
not a flat bucket): each pairs the contract with the registration extension that scans for
it, and — where the contract has a shape of its own to explain — its `SettingsBase<T>` /
`ISettingsSection` / `SettingsValidateOptions<T>` neighbors.

| Folder | Contract | Registration extension |
|---|---|---|
| `Endpoints/` | `IEndpoint` — `void MapEndpoint(IEndpointRouteBuilder app)` | `EndpointRegistration`: `AddEndpoints()`, `MapEndpoints()` |
| `Settings/` | `ISettings` (scan marker), `ISettingsSection` (`static abstract Section`), `SettingsBase<T>`, `SettingsValidateOptions<T>` | `SettingsRegistration`: `AddSettings()` |
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
| `AddRequestHandlers(assemblies)` | closed `IRequestHandler<,>` impls | the closed interface → impl (scoped, `TryAdd` — idempotent; two different handlers for one request type fail startup instead of last-one-wins) |
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

## `SettingsBase<T>`

A settings class derives from `SettingsBase<T>` (naming itself as `T`) **and** implements
`ISettingsSection`:

```csharp
public sealed class WeatherOptions : SettingsBase<WeatherOptions>, ISettingsSection
{
    public static string Section => "Weather";        // required — the build fails without it
    public string BaseAddress { get; init; } = "https://api.open-meteo.com";
    public int MaxForecastDays { get; init; } = 16;
}
```

That binds `appsettings.json`'s `"Weather": { … }` section, and `AddSettings()` picks it up —
nothing else to register. Inject `IOptions<WeatherOptions>` wherever it's needed.

### Compile-time guarantees

`SettingsBase<T>` is constrained `where T : SettingsBase<T>, ISettingsSection`, and
`ISettingsSection.Section` is `static abstract`. So:

| Mistake | Before | Now |
|---|---|---|
| Forgot `Section` | startup exception (reflection lookup) | **build error** (CS0311: `T` doesn't implement `ISettingsSection`) |
| `class FooOptions : SettingsBase<BarOptions>` (copy-paste) | silently registered `BarOptions` | startup exception (it compiles when `BarOptions` is itself valid, so `RegisterSettings` checks) |
| `Section => ""` | startup exception | startup exception (only the *value* can still be wrong) |

`ISettingsSection` is separate from `ISettings` because an abstract base class can't satisfy
a `static abstract` member for its subclasses — so each concrete settings class declares it.

### Validation: FluentValidation or DataAnnotations, chosen when it runs

Every settings type is validated on startup (`ValidateOnStart()`), by `SettingsValidateOptions<T>`:

- An `IValidator<T>` is registered → **FluentValidation**; every rule failure becomes its own
  startup error line (`WeatherOptions.MaxForecastDays: …`).
- No validator → the class's **DataAnnotations** attributes.

```csharp
// FluentValidation: just add a validator next to the settings class. The app's
// AddValidatorsFromAssemblies(...) registers it; nothing else changes.
internal sealed class WeatherOptionsValidator : AbstractValidator<WeatherOptions>
{
    public WeatherOptionsValidator()
    {
        RuleFor(x => x.BaseAddress).Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _));
        RuleFor(x => x.MaxForecastDays).InclusiveBetween(1, 16);
    }
}

// DataAnnotations: no validator, attributes on the properties instead.
public sealed class JwtOptions : SettingsBase<JwtOptions>, ISettingsSection
{
    public static string Section => "Jwt";

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;
}
```

The choice is made **when validation runs** (after the container is built), not when
`AddSettings()` runs — so it no longer matters whether validators are registered before or
after the settings. (It used to: registering validators afterwards silently fell back to
DataAnnotations, which for a FluentValidation-only class means *no* validation at all. That
ordering rule is gone; `SettingsBaseTests` pins it.) Prefer FluentValidation once a rule spans
properties (`TotalRequestTimeout > AttemptTimeout`) or needs more than an attribute can say.

### Collection properties: default to empty

The configuration binder *appends* to a collection that already has items, so a non-empty
initializer can never be narrowed from config — default collections to `[]` and apply the
real default where the value is used (see `Cors/README.md` for the bug this caused).

## Adding a feature

1. Create `Features/<Name>/` (in `AspNetDesignPatterns.Api`, not `KAM.Common`).
2. Add a request + `AbstractValidator`, a response DTO, an `IRequestHandler`, an `IEndpoint`.
3. Feature-owned services (typed client, repository)? Add an `IDependency`.
4. Reads config? Add a `SettingsBase<T>, ISettingsSection` class (+ a validator if you want
   FluentValidation).
5. Done — the scans pick all of it up, from either assembly.

## Tests

`tests/KAM.Common.Tests/DependencyInjection/` mirrors this folder's
shape — `Endpoints/EndpointRegistrationTests`, `Dependencies/DependencyRegistrationTests`,
`RequestHandlers/RequestHandlerRegistrationTests` (each scan, against fixtures declared in the
test assembly), `Settings/SettingsBaseTests` (both validation paths, FluentValidation still chosen when the
validator is registered *after* the settings, empty `Section` and a misnamed `T` fail fast, re-registering adds
one validator), `ExceptionHandling/GlobalExceptionHandlerTests` (500 + `ProblemDetails`, declines
gracefully). The scans *actually finding types across both assemblies* — the one thing a
KAM.Common-only test can't prove — is `ReflectionRegistrationTests` in
`tests/AspNetDesignPatterns.Api.Tests/Integration/`.
