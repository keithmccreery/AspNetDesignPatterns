namespace KAM.Common.Results;

/// <summary>
/// Classifies an <see cref="Error"/> so the HTTP layer can translate it to the right
/// status code without the domain layer knowing anything about HTTP.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,

    /// <summary>A downstream dependency (e.g. an upstream API) failed. Maps to 502.</summary>
    Upstream,
}

/// <summary>
/// An immutable description of why a <see cref="Result"/> failed. Prefer the categorized
/// factory methods so the <see cref="ErrorType"/> stays consistent with the intent; attach
/// the originating <see cref="System.Exception"/> and/or a <see cref="Context"/> payload with
/// <see cref="WithException"/> / <see cref="WithContext"/> so the boundary can log rich
/// diagnostics without the domain layer taking an <c>ILogger</c> dependency.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    /// <summary>The exception that caused this error, if any. Logged, never serialized to the client.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Structured context for logging (ids, inputs). Logged, never serialized to the client.</summary>
    public object? Context { get; init; }

    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Upstream(string code, string message) => new(code, message, ErrorType.Upstream);

    /// <summary>Returns a copy of this error carrying the originating exception.</summary>
    public Error WithException(Exception exception) => this with { Exception = exception };

    /// <summary>Returns a copy of this error carrying a structured logging context.</summary>
    public Error WithContext(object context) => this with { Context = context };
}

/// <summary>
/// An <see cref="Error"/> that carries per-field failures, produced when FluentValidation
/// rejects a request. Rendered as an RFC 9457 validation problem (HTTP 400).
/// </summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Failures)
    : Error("General.Validation", "One or more validation errors occurred.", ErrorType.Validation);
