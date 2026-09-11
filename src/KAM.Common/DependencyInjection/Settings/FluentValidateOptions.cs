using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.Options;

namespace KAM.Common.DependencyInjection;

/// <summary>
/// Adapts a FluentValidation <see cref="IValidator{T}"/> to <see cref="IValidateOptions{TOptions}"/>
/// so a settings class can be validated with the same validators used for request models.
/// Wired up by <see cref="SettingsBase{T}"/> only when a validator is registered.
/// </summary>
internal sealed class FluentValidateOptions<TOptions>(IServiceProvider serviceProvider)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Validators may themselves depend on scoped services.
        using IServiceScope scope = serviceProvider.CreateScope();
        IValidator<TOptions> validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();

        ValidationResult result = validator.Validate(options);
        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        IEnumerable<string> failures = result.Errors.Select(e =>
            $"{typeof(TOptions).Name}.{e.PropertyName}: {e.ErrorMessage}");

        return ValidateOptionsResult.Fail(failures);
    }
}
