# Shared/Handlers

The request-handler abstraction that keeps endpoints thin.

## Files

| File | Type | Purpose |
|---|---|---|
| `IRequestHandler.cs` | `IRequestHandler<in TRequest, TResponse>` | `Task<Result<TResponse>> HandleAsync(TRequest, CancellationToken)`. One request type, one response, always a `Result<T>`. |

## How it fits

- An **endpoint** resolves `IRequestHandler<TRequest, TResponse>` from DI and delegates
  straight to it — the endpoint stays a pure HTTP adapter (bind, validate, translate the
  `Result`).
- The **handler** holds the orchestration (often via a [pipeline](../Pipeline/README.md)) and
  is unit-testable without a web host.
- Implementations are **reflection-registered** as scoped by `AddRequestHandlers()` in
  `Program.cs` — see [`../../DependencyInjection`](../../DependencyInjection/README.md). No
  per-handler registration.

```csharp
internal sealed class GetForecastHandler(IPipelineFactory factory)
    : IRequestHandler<GetForecastRequest, ForecastResponse>
{
    public async Task<Result<ForecastResponse>> HandleAsync(GetForecastRequest request, CancellationToken ct)
    {
        // … compose + run a pipeline, return context.Response or the failure
    }
}

// endpoint
app.MapGet("/weather/forecast", async (
    [AsParameters] GetForecastRequest request,
    IRequestHandler<GetForecastRequest, ForecastResponse> handler,
    ILogger<GetForecastEndpoint> logger,
    CancellationToken ct) =>
        await handler.HandleAsync(request, ct).LogOnFailureAsync(logger).ToHttpResultAsync());
```

## Deliberately not a mediator

There's no `IMediator` / `ISender` indirection — the endpoint asks for the exact closed
handler interface it needs. Reflection registration gives the "no wiring per feature"
benefit; a dispatcher would add a runtime lookup and hide the dependency.

## Tests

The abstraction itself is an interface (nothing to test). Concrete handlers are tested per
feature — e.g. `tests/AspNetDesignPatterns.Api.Tests/Features/Weather/GetForecastHandlerTests.cs`.
Discovery/registration in isolation is
`tests/AspNetDesignPatterns.Api.Shared.Tests/DependencyInjection/RequestHandlers/RequestHandlerRegistrationTests.cs`;
against the real, two-assembly app it's
`tests/AspNetDesignPatterns.Api.Tests/Integration/ReflectionRegistrationTests.cs`.
