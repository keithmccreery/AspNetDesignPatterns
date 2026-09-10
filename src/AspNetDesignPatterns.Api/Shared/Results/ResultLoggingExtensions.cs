using System.Runtime.CompilerServices;

namespace AspNetDesignPatterns.Api.Shared.Results;

/// <summary>
/// Logs the outcome of a <see cref="Result"/> in one call, so a handler or endpoint can do
/// <c>result.LogOnFailure(logger).ToHttpResult()</c> without an <c>if</c>. Failures are logged
/// with the <see cref="Error"/>'s <see cref="Error.Exception"/> and <see cref="Error.Context"/>
/// when present.
/// </summary>
/// <remarks>
/// <see cref="Error.Context"/> is logged with structured destructuring (<c>{@Context}</c>).
/// The <c>ControlCharacterSanitizingEnricher</c> only sanitizes top-level scalar properties,
/// so a CR/LF string nested inside a context object is not stripped — keep context payloads
/// to ids and enums, not raw user input. (Accepted trade-off.)
/// </remarks>
public static class ResultLoggingExtensions
{
    /// <summary>Logs at Error level when <paramref name="result"/> is a failure. Returns it unchanged.</summary>
    public static TResult LogOnFailure<TResult>(
        this TResult result,
        ILogger logger,
        [CallerMemberName] string operation = "")
        where TResult : Result
    {
        if (result.IsSuccess)
        {
            return result;
        }

        var error = result.Error;

        logger.LogError(
            error.Exception,
            "{Operation} failed: {ErrorCode} — {ErrorMessage}{ContextSuffix}",
            operation,
            error.Code,
            error.Message,
            error.Context is null ? string.Empty : " (context attached)");

        if (error.Context is not null)
        {
            logger.LogError("{Operation} failure context: {@Context}", operation, error.Context);
        }

        return result;
    }

    /// <summary>Logs at <paramref name="level"/> when <paramref name="result"/> is a success. Returns it unchanged.</summary>
    public static TResult LogOnSuccess<TResult>(
        this TResult result,
        ILogger logger,
        LogLevel level = LogLevel.Information,
        [CallerMemberName] string operation = "")
        where TResult : Result
    {
        if (result.IsSuccess)
        {
            logger.Log(level, "{Operation} completed successfully", operation);
        }

        return result;
    }

    /// <summary>Logs either outcome — success at <paramref name="successLevel"/>, failure at Error.</summary>
    public static TResult LogResult<TResult>(
        this TResult result,
        ILogger logger,
        LogLevel successLevel = LogLevel.Information,
        [CallerMemberName] string operation = "")
        where TResult : Result
    {
        return result.IsSuccess
            ? result.LogOnSuccess(logger, successLevel, operation)
            : result.LogOnFailure(logger, operation);
    }

    /// <summary>
    /// Logs a failure with the calling file/member/line automatically captured — handy from
    /// deep in a pipeline where the operation name alone is not enough context.
    /// </summary>
    public static TResult LogError<TResult>(
        this TResult result,
        ILogger logger,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        where TResult : Result
    {
        if (result.IsSuccess)
        {
            return result;
        }

        var file = string.IsNullOrEmpty(filePath) ? "unknown" : Path.GetFileNameWithoutExtension(filePath);
        var error = result.Error;

        logger.LogError(
            error.Exception,
            "Error in {File}.{Member}:{Line} — {ErrorCode}: {ErrorMessage}",
            file,
            member,
            lineNumber,
            error.Code,
            error.Message);

        return result;
    }

    // ---- await-the-pipeline overloads (Task<Result> input) ----

    public static async Task<Result> LogOnFailureAsync(
        this Task<Result> resultTask,
        ILogger logger,
        [CallerMemberName] string operation = "") =>
        (await resultTask).LogOnFailure(logger, operation);

    public static async Task<Result<T>> LogOnFailureAsync<T>(
        this Task<Result<T>> resultTask,
        ILogger logger,
        [CallerMemberName] string operation = "") =>
        (await resultTask).LogOnFailure(logger, operation);

    public static async Task<Result> LogResultAsync(
        this Task<Result> resultTask,
        ILogger logger,
        LogLevel successLevel = LogLevel.Information,
        [CallerMemberName] string operation = "") =>
        (await resultTask).LogResult(logger, successLevel, operation);

    public static async Task<Result<T>> LogResultAsync<T>(
        this Task<Result<T>> resultTask,
        ILogger logger,
        LogLevel successLevel = LogLevel.Information,
        [CallerMemberName] string operation = "") =>
        (await resultTask).LogResult(logger, successLevel, operation);
}
