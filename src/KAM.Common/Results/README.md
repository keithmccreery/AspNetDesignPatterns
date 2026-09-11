# Results

The `Result` pattern: an operation that can fail returns a `Result` / `Result<T>` describing
the outcome instead of throwing.

## The layering rule

```
Endpoint    → returns IResult        (result.ToHttpResult())          — never throws
Handler     → returns Result<T>      (orchestration / pipeline)       — never throws
Service     → returns Result<T>      (maps upstream → contract)       — never throws; CATCHES client exceptions
Client      → returns T (or throws)  (HttpClient, DB, SDK)            — MAY throw; this is the only layer that does
```

A `Result` is for **expected** failure paths (not found, conflict, upstream down, validation).
Genuinely exceptional bugs still throw and are caught by the `GlobalExceptionHandler`
(→ 500 `ProblemDetails`).

## Files

| File | Purpose |
|---|---|
| `Result.cs` | `Result` and `sealed Result<T>` — the outcome type. |
| `Error.cs` | `Error` (record), `ErrorType` (category enum), `ValidationError` (per-field failures). |
| `ResultExtensions.cs` | `partial` — synchronous railway operators: `Match`, `Map`, `Bind`, `Ensure`, `Tap`, `TapError`. |
| `ResultExtensions.Async.cs` | `partial` — the async counterparts (three shapes each) + `ToHttpResultAsync`. |
| `ResultUtilities.cs` | Combinators over *several* results: `Combine`, `CombineAll`, `FirstSuccess`, `Ensure` (on a raw value). |
| `ResultHttpExtensions.cs` | `result.ToHttpResult()` → RFC 9457 `ProblemDetails` / `ValidationProblem`, plus `ErrorType.ToStatusCode()`. |
| `ResultLoggingExtensions.cs` | `result.LogOnFailure(logger)` / `LogOnSuccess` / `LogResult` / `LogError`, sync + async. |

## `Result` / `Result<T>` API

| Member | Notes |
|---|---|
| `Result.Success()` / `Result.Failure(error)` | Non-generic — for operations with no return value. |
| `Result.Success<T>(value)` / `Result.Failure<T>(error)` | Generic. |
| `result.IsSuccess` / `result.IsFailure` | |
| `result.Value` | **Throws `InvalidOperationException` on a failure** — check first, or… |
| `result.TryGetValue(out T value)` | `true` + non-null `value` on success. |
| `result.Error` | `Error.None` on success. |
| `result.ToResult()` | `Result<T>` → `Result` (drops the value, keeps success/failure). |
| implicit `T` → `Result<T>` | `return mappedValue;` from a `Result<T>` method. |
| implicit `Error` → `Result` / `Result<T>` | `return Error.NotFound(...);` from either. |

## `Error`

`Error` is a `record` — **value equality**. `Error.None` is the empty error carried by a success.

| Factory | `ErrorType` | Maps to |
|---|---|---|
| `Error.Validation(code, msg)` | `Validation` | 400 |
| `Error.Unauthorized(...)` | `Unauthorized` | 401 |
| `Error.Forbidden(...)` | `Forbidden` | 403 |
| `Error.NotFound(...)` | `NotFound` | 404 |
| `Error.Conflict(...)` | `Conflict` | 409 |
| `Error.Upstream(...)` | `Upstream` | 502 |
| `Error.Failure(code, msg)` | `Failure` | 500 |

Enrich without losing the original (returns a copy — `Error` is immutable):

```csharp
Error.Upstream("Weather.Down", "provider unavailable")
     .WithException(ex)                                   // logged, never serialized to the client
     .WithContext(new { q.Latitude, q.Longitude });       // logged, never serialized
```

`ValidationError : Error` carries `IReadOnlyDictionary<string, string[]> Failures` (field → messages).
`ToHttpResult()` renders it as a 400 `ValidationProblem`; anything that produces one
(`ValidationFilter`, `ValidateAsResult`, `ResultUtilities.CombineAll`) flows through the same body.

## Operator reference (sync — async is the same with `Async` suffix)

| Operator | Signature (abridged) | On success | On failure |
|---|---|---|---|
| `Match` | `Result<T>.Match(Func<T,R> ok, Func<Error,R> err) → R` | runs `ok` | runs `err` |
| `Map` | `Result<TIn>.Map(Func<TIn,TOut>) → Result<TOut>` | transforms the value | passes the failure through |
| `Bind` | `Result<TIn>.Bind(Func<TIn,Result<TOut>>) → Result<TOut>` | runs the next fallible step | short-circuits (next not run) |
| `Ensure` | `Result<T>.Ensure(Func<T,bool>, Error) → Result<T>` | fails with `Error` if predicate is false | passes through |
| `Tap` | `Result<T>.Tap(Action<T>) → Result<T>` | runs the side effect | no-op |
| `TapError` | `Result<T>.TapError(Action<Error>) → Result<T>` | no-op | runs the side effect |

`Bind` also has non-generic forms (`Result.Bind(Func<Result>)`, `Result<T>.Bind(Func<T,Result>)`,
`Result.Bind(Func<Result<TOut>>)`).

### Async shapes

Every async operator has **three** overloads so a chain never breaks out of the fluent style:

```csharp
result.MapAsync(x => FetchAsync(x))                     // Result<T>       + async projection
await task.MapAsync(x => x + 1)                          // Task<Result<T>> + sync projection
await task.BindAsync(LoadOrdersAsync)                    // Task<Result<T>> + async projection
```

## Worked example

```csharp
public async Task<Result<Receipt>> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
{
    return await _cartService.GetAsync(request.CartId, ct)           // Result<Cart>
        .EnsureAsync(cart => cart.Items.Count > 0,
                     Error.Validation("Cart.Empty", "the cart is empty"))
        .BindAsync(cart => _pricing.QuoteAsync(cart, ct))            // Result<Quote>
        .BindAsync(quote => _payments.ChargeAsync(quote, request.Card, ct))  // Result<Payment>
        .MapAsync(payment => Receipt.From(payment));                 // Result<Receipt>
    // any failure short-circuits; the first Error is what comes back
}

// endpoint
return await _handler.HandleAsync(request, ct)
    .LogOnFailureAsync(logger)      // logs Error.Exception + Error.Context if present
    .ToHttpResultAsync();           // 200 / 400 / 402 / 502 ProblemDetails
```

## `ResultUtilities` — aggregating several results

| Method | Use when |
|---|---|
| `Combine(r1, r2, …)` → `Result` | fail fast — return the **first** failure. |
| `Combine<T>(r1, r2, …)` → `Result<IReadOnlyList<T>>` | collect all values, or the first failure. |
| `CombineAll(r1, r2, …)` → `Result` | report **every** failure at once — folds them into one `ValidationError` keyed by error code. Form validation, batch input. |
| `FirstSuccess(items, selector)` → `Result<TOut>` | try each item; first success wins, else the last failure. |
| `Ensure(value, predicate, error)` / `Ensure(value, params (predicate, error)[])` | guard a raw value into a `Result<T>`. |

## Design decisions

**Category enum, not string inference.** `ErrorType` is compiler-checked and greppable.
Inferring the status from the error *code string* (`code.Contains("NOTFOUND")`) is fragile —
a typo silently becomes a 500, and there are no word boundaries. If you want string inference
it's a small change in `ResultHttpExtensions`; the enum is the recommended default.

**`.Value` throws on failure.** Returning `default!` would turn a logic bug into a
`NullReferenceException` far from its cause. Fail-fast at the access point instead.

**`Error.Context` is destructured in logs (`{@Context}`).** The
`ControlCharacterSanitizingEnricher` (see [`../Logging`](../Logging/README.md)) only sanitizes
**top-level scalar** properties, so a CR/LF string nested inside a context object is not
stripped. Keep context payloads to ids and enums, not raw user input. (Accepted trade-off.)

**Sync only had 8 methods; async adds ~40.** The async surface is the 3-shape matrix over the
same operations — mechanical, but it's what lets a real multi-step handler stay fluent. Split
into `ResultExtensions.Async.cs` so the core stays readable.

## Tests

`tests/KAM.Common.Tests/Results/` — `ResultTests` (factories, `.Value` throw, implicit conversions,
`ToResult`), `ErrorTests` (categorized factories, `.WithException/.WithContext` immutability,
`ValidationError`), `ResultCompositionTests` (every sync + async operator, short-circuit
behaviour, `ResultUtilities`), `ResultHttpExtensionsTests` (each `ErrorType` → status,
`ProblemDetails` / `ValidationProblem` shape), `ResultLoggingExtensionsTests` (log level
routing, exception + context, `[CallerMemberName]`, async).
