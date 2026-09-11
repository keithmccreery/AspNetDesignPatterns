using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AspNetDesignPatterns.Api.DependencyInjection;

/// <summary>
/// Last-resort handler for exceptions that escape a request. Anything a client throws should
/// be caught and converted to a <c>ProblemDetails</c> result at the Service layer; if
/// something slips through, this turns it into a 500 problem response (never a raw stack
/// trace) and logs it with the request's trace identifier.
/// </summary>
/// <remarks>
/// Uses <see cref="IProblemDetailsService"/> (not a hand-written <c>WriteAsJsonAsync</c>) so
/// the 500 body runs through the same <c>CustomizeProblemDetails</c> pipeline — and therefore
/// carries the same <c>traceId</c>/<c>instance</c> — as every other error response in the app.
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Please try again later or contact support.",
            },
        });
    }
}
