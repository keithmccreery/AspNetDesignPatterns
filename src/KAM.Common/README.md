# KAM.Common

The reusable half of the reference: everything a feature slice builds on, but nothing that
knows a feature exists. Every folder here is a top-level namespace under `KAM.Common.*`.

| Folder | Namespace | What it is |
|---|---|---|
| [`DependencyInjection/`](DependencyInjection/README.md) | `KAM.Common.DependencyInjection` | The reflection-registration conventions (`IEndpoint`, `ISettings`, `IDependency`, `IRequestHandler<,>` scans) and the global exception handler. |
| [`Results/`](Results/README.md) | `KAM.Common.Results` | `Result` / `Result<T>` + `Error`, railway-oriented composition, `Error → ProblemDetails`, result logging. |
| [`Pipeline/`](Pipeline/README.md) | `KAM.Common.Pipeline` | Middleware-style sequential pipeline over a shared context. |
| [`FluentValidations/`](FluentValidations/README.md) | `KAM.Common.FluentValidations` | Request validation at the HTTP edge and inside services. |
| [`Logging/`](Logging/README.md) | `KAM.Common.Logging` | CWE-117 log-injection hardening for Serilog. |
| [`Authentication/`](Authentication/README.md) | `KAM.Common.Authentication` | JWT bearer authentication, dev-token endpoint. |
| [`Authorization/`](Authorization/README.md) | `KAM.Common.Authorization` | Data-driven authorization policies (scopes/roles/claims/custom requirements from config) + the deny-by-default fallback. |
| [`Cors/`](Cors/README.md) | `KAM.Common.Cors` | Cross-Origin Resource Sharing, configured from `appsettings.json`. |
| [`HealthChecks/`](HealthChecks/README.md) | `KAM.Common.HealthChecks` | Liveness + readiness probes, tag-split, custom JSON writer. |
| [`Configuration/`](Configuration/README.md) | `KAM.Common.Configuration` | "Where/how am I running" view of the environment. |
| [`OpenApi/`](OpenApi/README.md) | `KAM.Common.OpenApi` | OpenAPI document + Scalar UI + JWT security scheme. |
| [`Handlers/`](Handlers/README.md) | `KAM.Common.Handlers` | The `IRequestHandler<TRequest, TResponse>` abstraction. |
| [`Telemetry/`](Telemetry/README.md) | `KAM.Common.Telemetry` | OpenTelemetry traces + metrics (OTLP), a shared `ActivitySource` for custom spans, and the settings the log sink shares with them. |

## Why this is a separate project — and why it's named `KAM.Common`, not `AspNetDesignPatterns.*`

Everything here is written to have **no reference to `AspNetDesignPatterns.Api`** — not the
host project, not a feature namespace, not `Program.cs`. That was already true when both
lived in one project (enforced by convention and, before the split, by architecture tests);
splitting them into their own project turns it into a **compile error** instead of a rule
that could quietly rot. `AspNetDesignPatterns.Api` takes a `ProjectReference` on this project;
this project takes none back.

It was first split out under the `AspNetDesignPatterns.Api.Shared` name — same namespace
family as the sample app, just one level down. That name turned out to be the wrong call:
`AspNetDesignPatterns.Api` is a *reference/sample* app, but Keith intends to reuse this
library on real projects sooner rather than later, and a namespace that reads as "part of the
sample" is actively misleading once that starts happening. Renaming it to **`KAM.Common`**
(Keith's initials) up front says what it actually is — an independent library that this repo
happens to consume — rather than deferring that until some hypothetical future extraction.
Flattening `DependencyInjection/` and the former `Shared/*` folders to be direct siblings
under `KAM.Common` (rather than `KAM.Common.Shared.*`) follows from the same reasoning: there
is no longer a second project's namespace to disambiguate against, so the extra `Shared`
segment was just noise.

## What moving here (originally) changed

Three internal types used to be referenced **by name** from `Program.cs`
(`GlobalExceptionHandler`, `BearerSecuritySchemeTransformer` — plus a public one,
`ControlCharacterSanitizingEnricher`, which needed no change). Across a project boundary an
internal type can't be named from outside, so the two internal ones are wired through their
own registration extension — `AddGlobalExceptionHandler()` and `AddBearerSecurityScheme()` —
the same shape as `AddJwtAuth()` or `AddHealthChecks()`. `Program.cs` never needed to know
these types existed; now it structurally can't.

**The one real bug this surfaced**: the reflection scans (`AddSettings`, `AddEndpoints`, …)
default to `Assembly.GetCallingAssembly()` when given none — which, once `JwtOptions` and
`DevTokenEndpoint` lived in a *different* assembly than the one calling `AddSettings()` /
`AddEndpoints()` from `Program.cs`, silently stopped discovering them. The app still started
(nothing failed loudly) but `/auth/token` 500'd on first use — `JwtOptions` was never bound,
so `SigningKey` was an empty string. Fixed by having `Program.cs` pass **both** assemblies
explicitly to every scan (`AddSettings`, `AddDependencies`, `AddEndpoints`,
`AddRequestHandlers`, `AddPipelineSteps`, and FluentValidation's own
`AddValidatorsFromAssemblies`) rather than relying on the single-assembly default. If you add
a third project that can define any of these, extend that same array.

## Tests

`tests/KAM.Common.Tests/` — its own project, referencing only this one (not
`AspNetDesignPatterns.Api`), for the same reason this is a separate project: tests for code
meant to be reused elsewhere shouldn't depend on the sample app it happens to be exercised by
today. `MapApiReference`'s Production/Development gating is proven against a real, unstarted
`WebApplication` in `OpenApi/OpenApiExposureTests.cs`, in this same project. End-to-end proof
that the two assemblies are wired together correctly (the reflection-scan bug above is exactly
the kind of thing a KAM.Common-only test can't catch), plus proof against the actual running
app (`ValidateRequest<T>` returning a 400, `MapApiReference` actually serving
`/openapi/v1.json`, …), lives in `tests/AspNetDesignPatterns.Api.Tests/Integration/`.
