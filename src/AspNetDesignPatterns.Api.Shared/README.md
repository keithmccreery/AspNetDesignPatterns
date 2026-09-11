# AspNetDesignPatterns.Api.Shared

The reusable half of the reference: everything a feature slice builds on, but nothing that
knows a feature exists. Two top-level concerns, kept as siblings on purpose:

| Folder | Namespace root | What it is |
|---|---|---|
| [`DependencyInjection/`](DependencyInjection/README.md) | `AspNetDesignPatterns.Api.DependencyInjection` | The reflection-registration conventions (`IEndpoint`, `ISettings`, `IDependency`, `IRequestHandler<,>` scans) and the global exception handler. |
| [`Shared/`](Shared/README.md) | `AspNetDesignPatterns.Api.Shared.*` | The cross-cutting building blocks — `Result`/`Error`, the pipeline, auth, health checks, OpenAPI, logging, FluentValidation glue, configuration. |

## Why this is a separate project

Everything here is written to have **no reference to `AspNetDesignPatterns.Api`** — not the
host project, not a feature namespace, not `Program.cs`. That was already true when both
lived in one project (enforced by convention and, before this split, by architecture tests);
splitting them into their own project turns it into a **compile error** instead of a rule
that could quietly rot. `AspNetDesignPatterns.Api` takes a `ProjectReference` on this project;
this project takes none back.

The practical payoff Keith is after: this is the shape the code needs to already be in if it
is ever pulled out into its own repo and shipped as a package — at that point it would become
**`Skimutt.Shared`**. Nothing here assumes that will happen (the namespaces stay
`AspNetDesignPatterns.Api.*` for now, matching the reference app, not a hypothetical package
name), but nothing here would need to change *shape* if it did — only the namespace, the
`.csproj`, and where it lives.

## What moving here changed

Three internal types used to be referenced **by name** from `Program.cs`
(`GlobalExceptionHandler`, `BearerSecuritySchemeTransformer` — plus a public one,
`ControlCharacterSanitizingEnricher`, which needed no change). Across a project boundary an
internal type can't be named from outside, so the two internal ones are now wired through
their own registration extension — `AddGlobalExceptionHandler()` and
`AddBearerSecurityScheme()` — the same shape as `AddJwtAuth()` or `AddHealthChecks()`.
`Program.cs` never needed to know these types existed; now it structurally can't.

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

`tests/AspNetDesignPatterns.Api.Shared.Tests/` — its own project, referencing only this one
(not `AspNetDesignPatterns.Api`), for the same reason this is a separate project: tests for
code meant to be extractable shouldn't depend on the app it happens to be embedded in today.
End-to-end proof that the two assemblies are wired together correctly (the bug above is
exactly the kind of thing a Shared-only test can't catch) lives in
`tests/AspNetDesignPatterns.Api.Tests/Integration/ReflectionRegistrationTests.cs`.
