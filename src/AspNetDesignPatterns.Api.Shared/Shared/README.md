# Shared

Cross-cutting building blocks used by every feature slice. One folder per concern; nothing
here knows about a specific feature. Each folder has its own README.

| Folder | Concern | Key entry point |
|---|---|---|
| [`Results/`](Results/README.md) | `Result` / `Result<T>` + `Error`, railway-oriented composition, `Error → ProblemDetails`, result logging | `Result`, `result.ToHttpResult()` |
| [`Pipeline/`](Pipeline/README.md) | Middleware-style sequential pipeline over a shared context | `IPipelineFactory` |
| [`FluentValidations/`](FluentValidations/README.md) | Request validation at the HTTP edge and inside services | `ValidateRequest<T>()`, `IValidator<T>.ValidateAsResult()` |
| [`Logging/`](Logging/README.md) | CWE-117 log-injection hardening for Serilog | `ControlCharacterSanitizingEnricher` |
| [`Auth/`](Auth/README.md) | JWT bearer auth, authorization policies, dev-token endpoint | `AddJwtAuth()` |
| [`HealthChecks/`](HealthChecks/README.md) | Liveness + readiness probes, tag-split, custom JSON writer | `MapAppHealthChecks()` |
| [`Configuration/`](Configuration/README.md) | "Where/how am I running" view of the environment | `AppEnvironment` |
| [`OpenApi/`](OpenApi/README.md) | OpenAPI document + Scalar UI + JWT security scheme | `MapApiReference()` |
| [`Handlers/`](Handlers/README.md) | The `IRequestHandler<TRequest, TResponse>` abstraction | `IRequestHandler<,>` |

The reflection-registration plumbing that wires all of this up at startup lives one level
out, in [`../DependencyInjection/`](../DependencyInjection/README.md).

## Testing

Every type here has unit tests under `tests/AspNetDesignPatterns.Api.Shared.Tests/<folder>/`
(a separate project from the app's own tests — see [`../README.md`](../README.md) for why),
mirroring this layout. `MapApiReference`'s Production/Development gating is proven against a
real, unstarted `WebApplication` in `OpenApi/OpenApiExposureTests.cs`, in this same project.
End-to-end proof against the actual running app (`ValidateRequest<T>` returning a 400,
`MapApiReference` actually serving `/openapi/v1.json`, …) lives in
`tests/AspNetDesignPatterns.Api.Tests/Integration/`.
