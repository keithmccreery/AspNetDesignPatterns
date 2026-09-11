using System.Diagnostics.CodeAnalysis;

namespace KAM.Common.Results;

/// <summary>
/// The outcome of an operation that can fail without throwing. Every layer at the Service
/// level and above returns a <see cref="Result"/> / <see cref="Result{T}"/>; only clients
/// (the lowest layer) are allowed to throw.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        switch (isSuccess)
        {
            case true when error != Error.None:
                throw new InvalidOperationException("A successful result cannot carry an error.");
            case false when error == Error.None:
                throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    /// <summary>Lets a method with return type <see cref="Result"/> simply <c>return someError;</c>.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>The outcome of an operation that yields a <typeparamref name="T"/> on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>The value. Throws if the result is a failure — check <see cref="Result.IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>Drops the value, keeping only the success/failure state — e.g. to return from a <c>Result</c> method.</summary>
    public Result ToResult() => IsSuccess ? Success() : Failure(Error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
