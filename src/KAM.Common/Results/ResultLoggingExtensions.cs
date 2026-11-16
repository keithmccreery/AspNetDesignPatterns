using System.Runtime.CompilerServices;

namespace KAM.Common.Results;

/// <summary>
/// Logs the outcome of a <see cref="Result"/> in one call, so a handler or endpoint can do
/// <c>result.LogOnFailure(logger).ToHttpResult()</c> without an <c>if</c>. Failures are logged
/// as one event carrying the <see cref="Error"/>'s <see cref="Error.Exception"/> and
/// <see cref="Error.Context"/> when present.
/// </summary>
/// <remarks>
/// <see cref="Error.Context"/> is logged with structured destructuring (<c>{@Context}</c>).
/// The <c>ControlCharacterSanitizingEnricher</c> only sanitizes top-level scalar properties,
/// so a CR/LF string nested inside a context object is not stripped — keep context payloads
/// to ids and enums, not raw user input. (Accepted trade-off.)
/// </remarks>
public static class ResultLoggingExtensions
{
    /// <summary>
    /// The level a failure of this type is logged at. Every failure is logged — the level says
    /// whose problem it is: <see cref="LogLevel.Error"/> for ours (an upstream outage, an
    /// unexpected failure: worth alerting on), <see cref="LogLevel.Warning"/> for
    /// security-relevant or state-related rejections (401/403 — a spike can mean probing or a
    /// broken client — and 409), and <see cref="LogLevel.Information"/> for the caller's own
    /// mistakes (400/404), which are routine and would otherwise drown real errors.
    /// </summary>
    public static LogLevel ToLogLevel(this ErrorType type) => type switch
    {
        ErrorType.Validation or ErrorType.NotFound => LogLevel.Information,
        ErrorType.Unauthorized or ErrorType.Forbidden or ErrorType.Conflict => LogLevel.Warning,
        _ => LogLevel.Error, // Upstream, Failure, and any type added later: fail loud
    };

    /// <summary>
    /// Logs <paramref name="result"/> when it's a failure, at <see cref="ToLogLevel"/> for its
    /// <see cref="ErrorType"/>, as a single event (exception and context included). Returns it
    /// unchanged.
    /// </summary>
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

        Error error = result.Error;
        LogLevel level = error.Type.ToLogLevel();

        // One event, so filtering on ErrorCode in a log store also finds the context.
        if (error.Context is null)
        {
            logger.Log(
                level,
                error.Exception,
                "{Operation} failed: {ErrorCode} — {ErrorMessage}",
                operation,
                error.Code,
                error.Message);
        }
        else
        {
            logger.Log(
                level,
                error.Exception,
                "{Operation} failed: {ErrorCode} — {ErrorMessage} {@Context}",
                operation,
                error.Code,
                error.Message,
                error.Context);
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

    /// <summary>Logs either outcome — success at <paramref name="successLevel"/>, failure as <see cref="LogOnFailure{TResult}"/> does.</summary>
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

        string file = string.IsNullOrEmpty(filePath) ? "unknown" : Path.GetFileNameWithoutExtension(filePath);
        Error error = result.Error;

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
