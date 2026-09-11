# Architecture tests

The conventions in `CLAUDE.md` and the `Shared/` / `Features/` READMEs, enforced as tests
([NetArchTest](https://github.com/BenMorris/NetArchTest)). They run in the normal
`dotnet test` pass — a broken rule fails the build like any other test.

| File | Rules |
|---|---|
| `ResultPatternTests.cs` | `*Service` / `*Handler` return `Result` from every public method; services and handlers never `throw`; only `*Client` types raise the feature exceptions. |
| `VerticalSliceTests.cs` | No feature depends on another feature. ("`Shared/`/`DependencyInjection/` never depend on a feature" used to be two more rules here — see "When a rule is wrong" below for why they're gone.) |
| `ConventionTests.cs` | Role types are named for their role (`…Endpoint`, `…Handler`, `…Step`, `…Options`, `…Validator`, `…HealthCheck`, `…Dependencies`) and sealed; endpoints don't reach past the handler into a service or client. |
| `SanityTests.cs` | Guards against a rule passing **vacuously** — every predicate the other fixtures rely on is asserted to match at least one real type. |

Everything in this folder scans `AspNetDesignPatterns.Api` only (see `ArchitectureRules.cs`)
— it has no visibility into `KAM.Common` (`DependencyInjection/`, `Auth/`, `Results/`, etc., a
separate project) unless a rule adds its own `Types.InAssembly` query; none currently do.

Two rules live outside this folder entirely because they need a running host, not static
analysis: `tests/AspNetDesignPatterns.Api.Tests/Integration/EndpointAuthorizationTests.cs`
walks the real app's mapped endpoints and requires each one to declare
`.RequireAuthorization(...)` or `.AllowAnonymous()` explicitly — whether an endpoint called
one is invisible to a type-level rule; it only shows up in the endpoint metadata built at
startup. `tests/KAM.Common.Tests/OpenApi/OpenApiExposureTests.cs` is the
same idea for the Production/Development gating on the OpenAPI document and Scalar UI.

## Pieces

- `ArchitectureRules.cs` — the assembly under test, the namespace-root constants, feature-slice
  discovery, and one assertion that names the offending types (NetArchTest's `TestResult`
  only reports pass/fail).
- `CustomRules.cs` — NetArchTest is type-level; these `ICustomRule` implementations read
  method signatures and IL for the rules that are about behaviour (returns `Result`, contains
  no `throw`, depends on a `*Service`/`*Client`). They skip compiler-generated members and
  fold in async state machines so an `async` method's `throw` is still seen.

## When a rule is wrong

A failing architecture test is sometimes the rule being too strict, not the code being
wrong. Fix whichever is actually at fault — don't reflexively loosen the rule. If a rule
genuinely cannot be expressed without false positives, delete it with a comment rather than
leaving it half-enforced:

- `Shared ⇏ DependencyInjection` was dropped early on for exactly this: the role interfaces
  live in `DependencyInjection/`, so `Shared` types that implement them legitimately
  reference that namespace.
- `Shared ⇏ Features` and `DependencyInjection ⇏ Features` were dropped later, for a
  different reason: once `Shared/` and `DependencyInjection/` moved into their own project
  (now `KAM.Common`, no reference back to this one), the rule stopped being
  something this suite could even check — the namespaces it queried for no longer exist in
  the assembly it scans. A rule can't fail meaningfully against an empty set; a **compile
  error** if anyone tried to add that reference back is a strictly stronger guarantee than
  the test ever was, so the rule was deleted rather than kept around passing vacuously.
