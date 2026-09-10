# Shared/Pipeline

A **middleware-style** pipeline for sequential work over a shared, mutable context. Each step
receives a `next` delegate, so a step can run code *before* and *after* the rest of the
chain, short-circuit by not calling `next`, or fail by returning `Result.Failure`.

## Files

| File | Type | Purpose |
|---|---|---|
| `IPipelineStep.cs` | `IPipelineStep<TContext>` | One step: `Task<Result> ExecuteAsync(TContext, Func<CancellationToken, Task<Result>> next, CancellationToken)`. |
| `Pipeline.cs` | `Pipeline<TContext>` | Holds an ordered list of step **types**, resolves each from DI at execution time, and runs the chain. Stops at the first failure or the first step that doesn't call `next`. Checks the `CancellationToken` before each step. |
| `IPipelineBuilder.cs` / `PipelineBuilder.cs` | fluent builder | `.Use<TStep>()` … `.Build()`. |
| `IPipelineFactory.cs` / `PipelineFactory.cs` | `IPipelineFactory` | `factory.CreateBuilder<TContext>()`. Inject this into a handler. |
| `PipelineServiceCollectionExtensions.cs` | registration | `AddPipeline()` registers the factory (scoped); `AddPipelineSteps(assemblies…)` reflection-registers every concrete `IPipelineStep<>` (scoped). Both are called once in `Program.cs`. |

## Usage

```csharp
// context: state threaded through the steps
internal sealed class ForecastContext(GetForecastRequest request)
{
    public GetForecastRequest Request { get; } = request;
    public ForecastResponse? Response { get; set; }
}

// a gate step: don't call next -> the rest of the chain is skipped
internal sealed class ClampWindowStep(IOptions<WeatherOptions> options) : IPipelineStep<ForecastContext>
{
    public Task<Result> ExecuteAsync(ForecastContext ctx, Func<CancellationToken, Task<Result>> next, CancellationToken ct)
        => ctx.Request.Days > options.Value.MaxForecastDays
            ? Task.FromResult(Result.Failure(Error.Validation("Weather.WindowTooLarge", "…")))
            : next(ct);
}

// a post-processing step: run AFTER the rest of the chain
internal sealed class RoundStep : IPipelineStep<ForecastContext>
{
    public async Task<Result> ExecuteAsync(ForecastContext ctx, Func<CancellationToken, Task<Result>> next, CancellationToken ct)
    {
        var result = await next(ct);
        if (result.IsSuccess) ctx.Response = Round(ctx.Response!);
        return result;
    }
}

// handler: compose per use case
internal sealed class GetForecastHandler(IPipelineFactory factory) : IRequestHandler<GetForecastRequest, ForecastResponse>
{
    public async Task<Result<ForecastResponse>> HandleAsync(GetForecastRequest request, CancellationToken ct)
    {
        var ctx = new ForecastContext(request);
        var pipeline = factory.CreateBuilder<ForecastContext>()
            .Use<ClampWindowStep>()
            .Use<FetchStep>()
            .Use<RoundStep>()
            .Build();

        var result = await pipeline.ExecuteAsync(ctx, ct);
        return result.IsFailure ? Result.Failure<ForecastResponse>(result.Error) : ctx.Response!;
    }
}
```

## Notes

- Steps are registered **scoped**, and so is `IPipelineFactory` — the pipeline resolves steps
  from the active request scope, so a step can safely depend on scoped services
  (`DbContext`, request-scoped state).
- Ordering is the order of `.Use<>()` calls, not an attribute.

## Tests

`tests/…/Shared/Pipeline/` — `PipelineTests` (wrap order, short-circuit, unwind on failure,
cancellation, empty pipeline), `PipelineRegistrationTests` (`AddPipeline` / `AddPipelineSteps`
reflection discovery, builder composition).
