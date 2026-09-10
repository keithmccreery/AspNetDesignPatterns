using FluentValidation;
using FluentValidation.Results;

namespace AspNetDesignPatterns.Api.Shared.FluentValidations;

/// <summary>
/// Endpoint filter that runs the registered <see cref="IValidator{T}"/> against the
/// <typeparamref name="TRequest"/> argument before the handler executes. A failure
/// short-circuits to a 400 validation <c>ProblemDetails</c>, so handlers never receive
/// an invalid request and never contain validation plumbing.
/// </summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest>? validator = null) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (validator is null)
        {
            return await next(context);
        }

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        ValidationResult validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (validation.IsValid)
        {
            return await next(context);
        }

        Dictionary<string, string[]> failures = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationError(failures).ToProblem();
    }
}
