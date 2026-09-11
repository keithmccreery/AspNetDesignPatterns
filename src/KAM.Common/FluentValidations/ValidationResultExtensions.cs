using FluentValidation;
using FluentValidation.Results;

using KAM.Common.Results;

namespace KAM.Common.FluentValidations;

/// <summary>
/// Bridges FluentValidation to the <see cref="Result"/> pattern for use <em>inside</em> a
/// service or pipeline step (the HTTP edge is handled by <see cref="ValidationFilter{TRequest}"/>).
/// A rejection becomes a <see cref="ValidationError"/>, so it still renders as an RFC 9457
/// validation problem with per-field errors if it reaches <c>ToHttpResult()</c>.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>Converts a FluentValidation <see cref="ValidationResult"/> to a <see cref="Result"/>.</summary>
    public static Result ToResult(this ValidationResult validationResult)
    {
        if (validationResult.IsValid)
        {
            return Result.Success();
        }

        Dictionary<string, string[]> failures = validationResult.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

        return new ValidationError(failures);
    }

    /// <summary>Validates <paramref name="instance"/> and returns the outcome as a <see cref="Result"/>.</summary>
    public static Result ValidateAsResult<T>(this IValidator<T> validator, T instance) =>
        validator.Validate(instance).ToResult();

    /// <summary>Async: validates <paramref name="instance"/> and returns the outcome as a <see cref="Result"/>.</summary>
    public static async Task<Result> ValidateAsResultAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default) =>
        (await validator.ValidateAsync(instance, cancellationToken)).ToResult();
}
