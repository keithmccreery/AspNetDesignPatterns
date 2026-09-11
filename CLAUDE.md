# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A **reference** ASP.NET Core Minimal API (`.NET 10`) that demonstrates the patterns and
standard practices used on real projects. It is meant to be read and copied from, so clarity
and consistency matter more than cleverness. It grows one vertical-slice feature at a time;
`Weather` is the reference slice that exercises every pattern.

## Commands

```bash
dotnet build                                  # builds both src projects; compiler warnings are errors
dotnet test                                   # NUnit, both test projects; ~180 tests (incl. NetArchTest arch rules)
dotnet format --verify-no-changes             # style + whitespace gate (CI)
dotnet format                                 # apply style fixes
dotnet run --project src/AspNetDesignPatterns.Api   # needs src/.../.env (copy .env.example)

# single test
dotnet test --filter "FullyQualifiedName~ResultCompositionTests"
```

Running the app: copy `src/AspNetDesignPatterns.Api/.env.example` → `.env`, set
`Jwt__SigningKey` (≥32 chars). Scalar UI at `/scalar`, OpenAPI at `/openapi/v1.json`,
probes at `/health/live` + `/health/ready`.
`GET /api/v1/weather/forecast` proxies the live open-meteo API (needs network); tests fake it.

## Architecture

- **Two projects.** `AspNetDesignPatterns.Api` is the host: `Program.cs` and `Features/`.
  `AspNetDesignPatterns.Api.Shared` is everything reusable — `DependencyInjection/` and
  `Shared/` — with **no reference back to the host**, on purpose: it's written to be
  extractable to its own package later (see its README). `Api` takes a `ProjectReference` on
  `Api.Shared`; never the other way round.
- **Vertical slices** under `Features/<Name>/` (in `AspNetDesignPatterns.Api`) — each owns its
  endpoint, request+validator, response DTO, handler, service, client, options, pipeline steps.
- **`Shared/`** (in `AspNetDesignPatterns.Api.Shared`) — cross-cutting building blocks, one
  folder per concern, each with a README. Infrastructure that is genuinely cross-cutting
  (auth, OpenAPI, health checks) *is* wired in `Program.cs`; only *feature* registration goes
  through the reflection scans / `IDependency`. A type a reflection scan needs to see from
  `Program.cs` but that should stay `internal` gets its own registration extension (e.g.
  `AddGlobalExceptionHandler()`) rather than being made `public` just to be nameable across
  the project boundary.
- **`DependencyInjection/`** (in `AspNetDesignPatterns.Api.Shared`) — the reflection-registration
  conventions, one folder per concern (`Endpoints/`, `Settings/`, `Dependencies/`,
  `RequestHandlers/`, `ExceptionHandling/`) — see its README. **Every scan takes an explicit
  `params Assembly[]`, and `Program.cs` always passes both projects' assemblies** — a type
  can live in either one, and the zero-argument default (`Assembly.GetCallingAssembly()`)
  only sees the assembly that called it. Missing this is a real, silent failure mode (a
  settings/endpoint/handler/dependency/step in the assembly *not* passed is never discovered,
  with no error at startup) — extend the array in `Program.cs`, don't rely on the default,
  if a third assembly is ever added.
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
8. **Every mapped endpoint declares its auth intent explicitly** — `.RequireAuthorization(...)`
   or `.AllowAnonymous()`, never neither. Enforced twice: `EndpointAuthorizationTests`
   (`tests/…/Integration/`) fails the build if one doesn't, and `FallbackPolicy` denies by
   default at runtime as the backstop if that test is ever bypassed.

## Dependencies

Prefer a type already in the BCL / ASP.NET Core shared framework over a new NuGet package
when it solves the same problem — e.g. `Microsoft.AspNetCore.WebUtilities.QueryHelpers` for a
query string rather than Flurl, `IMemoryCache` rather than a hand-rolled cache. Reach for a
third-party package when it's genuinely the standard tool for the job (FluentValidation,
Polly, NetArchTest), not by default. This matters more as the repo grows into OpenTelemetry
and cloud-provider SDKs — check the framework first.

## Style (enforced by `.editorconfig` + `dotnet format`)

- File-scoped namespaces, always braces, explicit types everywhere — **never `var`** (the
  analyzer rule is advisory, but the codebase has zero `var`; use target-typed `new()` when
  the type is on the left). For a `throws`-assertion lambda use `Func<T>`, not `Action`, so
  the constructed instance is still "used" (avoids CA1806).
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

Promoted to `warning` (see the `.editorconfig` block for the full list): the AsyncFixer /
VSTHRD async-correctness set, `MA0040`/`MA0079` (flow the `CancellationToken`), `MA0002`
(explicit `StringComparer`), `MA0076` (no implicit culture `ToString`), and `VSTHRD200`
(`Async` suffix). `VSTHRD200`, `MA0002`, `MA0076` are turned back off in the test-project
override block — a test name is a sentence, and test fixtures use invariant literals. The
same block also silences the XML-doc completeness/style family (`RCS1141`, `SA1611`,
`SA1623`, `MA0219`, …) since test types aren't public API. `IDE0058` is off globally
(fluent `.Should()` / DI chains, all noise). The build and
`dotnet format analyzers --severity warn` are both expected to be clean.

## Tests

- **NUnit** + AwesomeAssertions + NSubstitute. `AwesomeAssertions` is the free
  FluentAssertions fork; `NSubstitute` (not Moq).
- Test tree mirrors `src/`. Every shared service has a focused unit-test file.
- Every test body is sectioned with `// Arrange`, `// Act`, `// Assert` comments, in that
  order. Fuse the labels when the steps fuse: `// Arrange & Act`, `// Act & Assert`, or
  `// Arrange & Act & Assert` for a single fluent line.
- Integration tests: one shared `WeatherApiFactory` owned by `GlobalTestSetup` (`[SetUpFixture]`)
  — Serilog's two-stage init freezes the static logger on host build, so a single host keeps
  it deterministic.
- **Two test projects**, mirroring the two src projects — `AspNetDesignPatterns.Api.Tests`
  references `AspNetDesignPatterns.Api` (and transitively `.Shared`); `AspNetDesignPatterns.Api.Shared.Tests`
  references only `.Shared`, never the host, for the same reason `.Shared` doesn't reference
  the host. Each has its own `TestSupport/` — `StubHttpMessageHandler` in the Api one;
  `FakeHostEnvironment` and `ManageEnvironmentVariables` in the Shared one — split by which
  project's tests actually use them, not duplicated.
- `tests/…/Architecture/` (in `AspNetDesignPatterns.Api.Tests`) enforces the conventions in
  this file with NetArchTest (services return `Result`, only clients throw, no cross-feature
  deps, role types named + sealed) over the `AspNetDesignPatterns.Api` assembly only — it
  can't see into `.Shared` without its own `Types.InAssembly` query, and none currently do.
  A failing arch test may mean the rule is too strict — fix whichever is at fault; if a rule
  can't avoid false positives (or, as happened when `Shared`/`DependencyInjection` moved to
  their own project, becomes impossible to even express meaningfully), delete it with a
  comment. See its README. Rules that need a running host rather than static analysis (every
  endpoint declares an auth intent; the OpenAPI/Scalar exposure gating) live as ordinary tests
  next to what they check instead.

## When adding a feature

Copy the shape of `Features/Weather/` and its README. Add the 5 pieces (request+validator,
response, handler, endpoint, and — if needed — `IDependency` / `SettingsBase<T>` / pipeline
steps), plus a matching test folder. Nothing else to wire.
