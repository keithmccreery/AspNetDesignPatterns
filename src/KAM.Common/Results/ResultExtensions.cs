namespace KAM.Common.Results;

/// <summary>
/// Railway-oriented composition for <see cref="Result"/> / <see cref="Result{T}"/>: chain
/// operations that each may fail, without unwinding into nested <c>if (result.IsSuccess)</c>
/// blocks. A failure anywhere in the chain short-circuits the rest.
/// </summary>
/// <remarks>
/// This file holds the synchronous operators. The <c>Task</c>-returning overloads — async
/// projections, and chaining onto a <c>Task&lt;Result&gt;</c> without an intermediate
/// <c>await</c> — live in <c>ResultExtensions.Async.cs</c>.
/// </remarks>
public static partial class ResultExtensions
{
    // ---- Match: collapse a Result into a single value ----

    /// <summary>Runs <paramref name="onSuccess"/> or <paramref name="onFailure"/> and returns its value.</summary>
    public static TOut Match<T, TOut>(
        this Result<T> result,
        Func<T, TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);

    // ---- Map: transform the value of a success; pass failures through ----

    /// <summary>Projects the value of a successful result; a failure is returned unchanged.</summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error);

    // ---- Bind: chain an operation that itself returns a Result ----

    /// <summary>Runs <paramref name="next"/> only if <paramref name="result"/> succeeded.</summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next) =>
        result.IsSuccess ? next(result.Value) : Result.Failure<TOut>(result.Error);

    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> next) =>
        result.IsSuccess ? next(result.Value) : Result.Failure(result.Error);

    public static Result Bind(this Result result, Func<Result> next) =>
        result.IsSuccess ? next() : result;

    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> next) =>
        result.IsSuccess ? next() : Result.Failure<TOut>(result.Error);

    // ---- Ensure: fail a success that doesn't satisfy a predicate ----

    /// <summary>Turns a success into a failure when <paramref name="predicate"/> is not met.</summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error) =>
        result.IsFailure || predicate(result.Value) ? result : Result.Failure<T>(error);

    // ---- Tap: run a side effect without changing the result ----

    /// <summary>Runs <paramref name="action"/> on the value of a success, then returns the result unchanged.</summary>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    public static Result Tap(this Result result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }

    /// <summary>Runs <paramref name="action"/> on the error of a failure, then returns the result unchanged.</summary>
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    public static Result TapError(this Result result, Action<Error> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }
}
