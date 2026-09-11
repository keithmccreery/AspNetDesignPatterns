using Microsoft.AspNetCore.Builder;

namespace KAM.Common.FluentValidations;

public static class ValidationEndpointExtensions
{
    /// <summary>
    /// Adds the FluentValidation <see cref="ValidationFilter{TRequest}"/> for
    /// <typeparamref name="TRequest"/> and advertises the 400 response in OpenAPI.
    /// </summary>
    public static RouteHandlerBuilder ValidateRequest<TRequest>(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
