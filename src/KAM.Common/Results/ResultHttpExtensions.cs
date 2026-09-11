using Microsoft.AspNetCore.Http;

namespace KAM.Common.Results;

/// <summary>
/// Central translation from a domain <see cref="Error"/> to an HTTP status code and an
/// RFC 9457 <c>ProblemDetails</c> payload. This is what lets every endpoint return
/// <c>result.ToHttpResult()</c> and get consistent, spec-compliant error bodies — the same
/// shape the <c>GlobalExceptionHandler</c> produces for uncaught exceptions.
/// </summary>
public static class ResultHttpExtensions
{
    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Upstream => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string ToTitle(this ErrorType type) => type switch
    {
        ErrorType.Validation => "One or more validation errors occurred.",
        ErrorType.Unauthorized => "Authentication is required.",
        ErrorType.Forbidden => "You do not have access to this resource.",
        ErrorType.NotFound => "The requested resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state.",
        ErrorType.Upstream => "An upstream dependency failed.",
        _ => "An unexpected error occurred.",
    };

    public static IResult ToProblem(this Error error)
    {
        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(validation.Failures, title: error.Type.ToTitle());
        }

        return TypedResults.Problem(
            title: error.Type.ToTitle(),
            detail: error.Message,
            statusCode: error.Type.ToStatusCode(),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = error.Code });
    }

    /// <summary>Bridges a non-generic <see cref="Result"/> to a Minimal API return value.</summary>
    public static IResult ToHttpResult(this Result result, Func<IResult>? onSuccess = null) =>
        result.IsSuccess ? onSuccess?.Invoke() ?? TypedResults.NoContent() : result.Error.ToProblem();

    /// <summary>Bridges a <see cref="Result{T}"/> to a Minimal API return value.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? TypedResults.Ok(result.Value)
            : result.Error.ToProblem();
}
