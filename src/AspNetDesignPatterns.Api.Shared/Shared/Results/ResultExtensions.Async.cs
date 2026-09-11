using Microsoft.AspNetCore.Http;

namespace AspNetDesignPatterns.Api.Shared.Results;

/// <summary>
/// Asynchronous counterparts to the operators in <c>ResultExtensions.cs</c>. For every
/// operation there are three shapes, so a chain never has to break out of the fluent style
/// to <c>await</c> an intermediate step:
/// <list type="number">
///   <item><c>Result&lt;T&gt;</c> + an async projection <c>Func&lt;…, Task&lt;…&gt;&gt;</c></item>
///   <item>a <c>Task&lt;Result&lt;T&gt;&gt;</c> + a synchronous projection</item>
///   <item>a <c>Task&lt;Result&lt;T&gt;&gt;</c> + an async projection</item>
/// </list>
/// Example: <c>await FetchUserAsync(id).BindAsync(LoadOrdersAsync).MapAsync(Summarize)</c>.
/// </summary>
public static partial class ResultExtensions
{
    // ======================================================================
    //  MatchAsync
    // ======================================================================

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Result<T> result,
        Func<T, Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure) =>
        result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Error);

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Result<T>> resultTask,
        Func<T, TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Result<T>> resultTask,
        Func<T, Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        Result<T> result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }

    public static async Task<TOut> MatchAsync<TOut>(
        this Result result,
        Func<Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure) =>
        result.IsSuccess ? await onSuccess() : await onFailure(result.Error);

    public static async Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);

    public static async Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask,
        Func<Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        Result result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }

    // ======================================================================
    //  MapAsync
    // ======================================================================

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<TOut>> map) =>
        result.IsSuccess ? Result.Success(await map(result.Value)) : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> map) =>
        (await resultTask).Map(map);

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<TOut>> map)
    {
        Result<TIn> result = await resultTask;
        return await result.MapAsync(map);
    }

    // ======================================================================
    //  BindAsync -- Result<TIn> -> Result<TOut>
    // ======================================================================

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> next) =>
        result.IsSuccess ? await next(result.Value) : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result<TOut>> next) =>
        (await resultTask).Bind(next);

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> next)
    {
        Result<TIn> result = await resultTask;
        return await result.BindAsync(next);
    }

    // ======================================================================
    //  BindAsync -- Result<TIn> -> Result (non-generic)
    // ======================================================================

    public static async Task<Result> BindAsync<TIn>(
        this Result<TIn> result,
        Func<TIn, Task<Result>> next) =>
        result.IsSuccess ? await next(result.Value) : Result.Failure(result.Error);

    public static async Task<Result> BindAsync<TIn>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result> next) =>
        (await resultTask).Bind(next);

    public static async Task<Result> BindAsync<TIn>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result>> next)
    {
        Result<TIn> result = await resultTask;
        return await result.BindAsync(next);
    }

    // ======================================================================
    //  BindAsync -- Result (non-generic) -> Result / Result<TOut>
    // ======================================================================

    public static async Task<Result> BindAsync(
        this Result result,
        Func<Task<Result>> next) =>
        result.IsSuccess ? await next() : result;

    public static async Task<Result> BindAsync(
        this Task<Result> resultTask,
        Func<Result> next) =>
        (await resultTask).Bind(next);

    public static async Task<Result> BindAsync(
        this Task<Result> resultTask,
        Func<Task<Result>> next)
    {
        Result result = await resultTask;
        return result.IsSuccess ? await next() : result;
    }

    public static async Task<Result<TOut>> BindAsync<TOut>(
        this Result result,
        Func<Task<Result<TOut>>> next) =>
        result.IsSuccess ? await next() : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> BindAsync<TOut>(
        this Task<Result> resultTask,
        Func<Result<TOut>> next) =>
        (await resultTask).Bind(next);

    public static async Task<Result<TOut>> BindAsync<TOut>(
        this Task<Result> resultTask,
        Func<Task<Result<TOut>>> next)
    {
        Result result = await resultTask;
        return result.IsSuccess ? await next() : Result.Failure<TOut>(result.Error);
    }

    // ======================================================================
    //  EnsureAsync
    // ======================================================================

    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        Error error) =>
        result.IsFailure || await predicate(result.Value) ? result : Result.Failure<T>(error);

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        Error error) =>
        (await resultTask).Ensure(predicate, error);

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task<bool>> predicate,
        Error error)
    {
        Result<T> result = await resultTask;
        return await result.EnsureAsync(predicate, error);
    }

    // ======================================================================
    //  TapAsync / TapErrorAsync
    // ======================================================================

    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess)
        {
            await action(result.Value);
        }

        return result;
    }

    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Action<T> action) =>
        (await resultTask).Tap(action);

    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
    {
        Result<T> result = await resultTask;
        return await result.TapAsync(action);
    }

    public static async Task<Result> TapAsync(this Result result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action();
        }

        return result;
    }

    public static async Task<Result> TapAsync(this Task<Result> resultTask, Action action) =>
        (await resultTask).Tap(action);

    public static async Task<Result> TapAsync(this Task<Result> resultTask, Func<Task> action)
    {
        Result result = await resultTask;
        return await result.TapAsync(action);
    }

    public static async Task<Result<T>> TapErrorAsync<T>(this Result<T> result, Func<Error, Task> action)
    {
        if (result.IsFailure)
        {
            await action(result.Error);
        }

        return result;
    }

    public static async Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Action<Error> action) =>
        (await resultTask).TapError(action);

    public static async Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task> action)
    {
        Result<T> result = await resultTask;
        return await result.TapErrorAsync(action);
    }

    public static async Task<Result> TapErrorAsync(this Result result, Func<Error, Task> action)
    {
        if (result.IsFailure)
        {
            await action(result.Error);
        }

        return result;
    }

    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Action<Error> action) =>
        (await resultTask).TapError(action);

    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Func<Error, Task> action)
    {
        Result result = await resultTask;
        return await result.TapErrorAsync(action);
    }

    // ======================================================================
    //  ToHttpResultAsync -- await the pipeline, then translate (see ResultHttpExtensions)
    // ======================================================================

    public static async Task<IResult> ToHttpResultAsync(this Task<Result> resultTask) =>
        (await resultTask).ToHttpResult();

    public static async Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask) =>
        (await resultTask).ToHttpResult();
}
