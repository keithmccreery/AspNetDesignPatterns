namespace KAM.Common.Results;

/// <summary>
/// Combinators over <em>several</em> results: aggregate them, pick the first success, or
/// guard a raw value into a <see cref="Result{T}"/>.
/// </summary>
public static class ResultUtilities
{
    /// <summary>Succeeds only if every input succeeded; otherwise returns the <b>first</b> failure.</summary>
    public static Result Combine(params IReadOnlyList<Result> results)
    {
        foreach (Result result in results)
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Succeeds with the list of all values only if every input succeeded; otherwise returns
    /// the first failure.
    /// </summary>
    public static Result<IReadOnlyList<T>> Combine<T>(params IReadOnlyList<Result<T>> results)
    {
        List<T> values = new(results.Count);

        foreach (Result<T> result in results)
        {
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<T>>(result.Error);
            }

            values.Add(result.Value);
        }

        return Result.Success<IReadOnlyList<T>>(values);
    }

    /// <summary>
    /// Succeeds only if every input succeeded; otherwise returns a single
    /// <see cref="ValidationError"/> aggregating <b>all</b> failures (keyed by error code).
    /// Use this when the caller should see every problem at once (form validation, batch input).
    /// </summary>
    public static Result CombineAll(params IReadOnlyList<Result> results)
    {
        Dictionary<string, string[]> failures = results
            .Where(r => r.IsFailure)
            .GroupBy(r => r.Error.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Error.Message).ToArray(), StringComparer.Ordinal);

        return failures.Count == 0 ? Result.Success() : new ValidationError(failures);
    }

    /// <summary>
    /// Applies <paramref name="selector"/> to each item and returns the first success; if none
    /// succeed, returns the last failure (or a "no items" error for an empty sequence).
    /// </summary>
    public static Result<TOut> FirstSuccess<TIn, TOut>(
        IEnumerable<TIn> items,
        Func<TIn, Result<TOut>> selector)
    {
        Result<TOut>? lastFailure = null;

        foreach (TIn item in items)
        {
            Result<TOut> result = selector(item);
            if (result.IsSuccess)
            {
                return result;
            }

            lastFailure = result;
        }

        return lastFailure
            ?? Result.Failure<TOut>(Error.Failure("Result.NoItems", "No items were provided."));
    }

    /// <summary>Wraps <paramref name="value"/> in a success, or the given error when the predicate fails.</summary>
    public static Result<T> Ensure<T>(T value, Func<T, bool> predicate, Error error) =>
        predicate(value) ? Result.Success(value) : Result.Failure<T>(error);

    /// <summary>Runs several guards over <paramref name="value"/>, returning the first that fails.</summary>
    public static Result<T> Ensure<T>(T value, params IReadOnlyList<(Func<T, bool> Predicate, Error Error)> guards)
    {
        foreach ((Func<T, bool> predicate, Error error) in guards)
        {
            if (!predicate(value))
            {
                return Result.Failure<T>(error);
            }
        }

        return Result.Success(value);
    }
}
