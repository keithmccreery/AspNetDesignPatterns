# Architecture tests

The conventions in `CLAUDE.md` and the `Shared/` / `Features/` READMEs, enforced as tests
([NetArchTest](https://github.com/BenMorris/NetArchTest)). They run in the normal
`dotnet test` pass — a broken rule fails the build like any other test.

| File | Rules |
|---|---|
| `ResultPatternTests.cs` | `*Service` / `*Handler` return `Result` from every public method; services and handlers never `throw`; only `*Client` types raise the feature exceptions. |
| `VerticalSliceTests.cs` | No feature depends on another feature; `Shared/` and the `DependencyInjection/` plumbing never depend on a feature. |
| `ConventionTests.cs` | Role types are named for their role (`…Endpoint`, `…Handler`, `…Step`, `…Options`, `…Validator`, `…HealthCheck`, `…Dependencies`) and sealed; endpoints don't reach past the handler into a service or client. |
| `SanityTests.cs` | Guards against a rule passing **vacuously** — every predicate the other fixtures rely on is asserted to match at least one real type. |

One rule lives outside this folder because it needs a running host, not static analysis:
`tests/…/Integration/EndpointAuthorizationTests.cs` walks the real app's mapped endpoints and
requires each one to declare `.RequireAuthorization(...)` or `.AllowAnonymous()` explicitly —
whether an endpoint called one is invisible to a type-level rule; it only shows up in the
endpoint metadata built at startup. `OpenApiExposureTests.cs` (`Shared/OpenApi/`) is the same
idea for the Production/Development gating on the OpenAPI document and Scalar UI.

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
leaving it half-enforced (`Shared ⇏ DependencyInjection` was dropped for exactly this: the
role interfaces live in `DependencyInjection/`, so `Shared` types that implement them
legitimately reference that namespace).
