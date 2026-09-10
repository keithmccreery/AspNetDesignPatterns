# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A **reference** ASP.NET Core Minimal API (`.NET 10`) that demonstrates the patterns and
standard practices used on real projects. It is meant to be read and copied from, so clarity
and consistency matter more than cleverness. It grows one vertical-slice feature at a time;
`Weather` is the reference slice that exercises every pattern.

## Commands

```bash
dotnet build                                  # analyzers run here; compiler warnings are errors
dotnet test                                   # NUnit; 113 tests
dotnet format --verify-no-changes             # style + whitespace gate (CI)
dotnet format                                 # apply style fixes
dotnet run --project src/AspNetDesignPatterns.Api   # needs src/.../.env (copy .env.example)

# single test
dotnet test --filter "FullyQualifiedName~ResultCompositionTests"
```

Running the app: copy `src/AspNetDesignPatterns.Api/.env.example` → `.env`, set
`Jwt__SigningKey` (≥32 chars). Scalar UI at `/scalar`, OpenAPI at `/openapi/v1.json`.
`GET /api/v1/weather/forecast` proxies the live open-meteo API (needs network); tests fake it.

## Architecture

- **Vertical slices** under `Features/<Name>/` — each owns its endpoint, request+validator,
  response DTO, handler, service, client, options, pipeline steps.
- **`Shared/`** — cross-cutting building blocks, one folder per concern, each with a README.
- **`DependencyInjection/`** — reflection-registration plumbing (see its README).
- **Request flow**: endpoint (thin HTTP adapter) → `IRequestHandler<TReq,TRes>` → optional
  `Pipeline<TContext>` → service (returns `Result<T>`) → typed client (may throw).
- **`Program.cs`** is a deliberately flat, banner-organized composition root — **do not**
  extract it into `AddApiServices()` helpers. Feature/Shared registration is already
  factored out into the reflection scans.

## Non-negotiable conventions

1. **`Result` / `Result<T>` above the client layer.** Services and handlers never throw
   upward; they catch client exceptions and return `Error.Upstream(...).WithException(ex)`.
   Only the client (HttpClient/DB/SDK) throws. See `Shared/Results/README.md`.
2. **Endpoints return `ProblemDetails`** via `result.ToHttpResult()` — never a bespoke error
   shape.
3. **No new registration in `Program.cs` per feature.** Add an `IEndpoint`, `IRequestHandler`,
   `SettingsBase<T>`, `IPipelineStep<>`, or an `IDependency` module — the scans pick it up.
4. **Settings** = a `SettingsBase<T>` subclass with `public static string Section`. It
   self-registers, binds, and validates on startup (FluentValidation if a validator exists,
   else DataAnnotations).
5. **Secrets** come from `.env` (`Jwt__SigningKey`), never a committed `appsettings.json`.
6. **`ILogger<T>` only** — Serilog is the implementation detail. Services generally take no
   logger; they attach context to the `Error` and the boundary calls `result.LogOnFailure(logger)`.
7. **Untrusted strings in logs**: the `ControlCharacterSanitizingEnricher` handles top-level
   scalars globally; don't put raw user input inside destructured (`{@obj}`) log payloads.

## Style (enforced by `.editorconfig` + `dotnet format`)

- File-scoped namespaces, always braces, explicit types (not `var` — advisory, but match it).
- **No primary constructors** — team preference; classic constructors with explicit fields.
- Non-public `const` → `SCREAMING_CASE`; `public const` → PascalCase (BCL convention).
- Trailing newline on every file.
- Test names: `Method_under_test_expected_behaviour` (underscores OK in test project).

## Analyzers

`Directory.Build.props` adds Roslynator, Meziantou, StyleCop, AsyncFixer,
IDisposableAnalyzers, VS Threading, ErrorProne.NET. Baseline is `suggestion`
(`dotnet_analyzer_diagnostic.severity` in `.editorconfig`); specific high-signal rules are
promoted to `warning`. Promote more as the codebase adopts them — don't silence to make a
build pass without understanding the rule.

## Tests

- **NUnit** + AwesomeAssertions + NSubstitute. `AwesomeAssertions` is the free
  FluentAssertions fork; `NSubstitute` (not Moq).
- Test tree mirrors `src/`. Every shared service has a focused unit-test file.
- Integration tests: one shared `WeatherApiFactory` owned by `GlobalTestSetup` (`[SetUpFixture]`)
  — Serilog's two-stage init freezes the static logger on host build, so a single host keeps
  it deterministic.
- `tests/…/TestSupport/` holds shared doubles (`FakeHostEnvironment`, `ManageEnvironmentVariables`).

## When adding a feature

Copy the shape of `Features/Weather/` and its README. Add the 5 pieces (request+validator,
response, handler, endpoint, and — if needed — `IDependency` / `SettingsBase<T>` / pipeline
steps), plus a matching test folder. Nothing else to wire.
