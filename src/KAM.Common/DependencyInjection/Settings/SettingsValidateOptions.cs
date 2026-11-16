using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.Options;

namespace KAM.Common.DependencyInjection;

/// <summary>
/// Validates a <see cref="SettingsBase{T}"/> instance: with the registered FluentValidation
/// <see cref="IValidator{T}"/> if there is one, otherwise with its DataAnnotations attributes.
/// </summary>
/// <remarks>
/// The choice is made here, at validation time, rather than when <c>AddSettings()</c> runs: by
/// the time <c>ValidateOnStart</c> validates, the container is built and every validator is
/// registered, whatever order <c>Program.cs</c> called things in.
/// </remarks>
internal sealed class SettingsValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IServiceProvider _serviceProvider;

    public SettingsValidateOptions(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // SettingsBase<T> binds only the default (unnamed) instance.
        if (!string.Equals(name, Options.DefaultName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        // Validators may themselves depend on scoped services (and FluentValidation's
        // assembly scan registers them scoped by default).
        using IServiceScope scope = _serviceProvider.CreateScope();
        IValidator<TOptions>? validator = scope.ServiceProvider.GetService<IValidator<TOptions>>();

        return validator is null
            ? new DataAnnotationValidateOptions<TOptions>(name).Validate(name, options)
            : ValidateWithFluentValidation(validator, options);
    }

    private static ValidateOptionsResult ValidateWithFluentValidation(IValidator<TOptions> validator, TOptions options)
    {
        ValidationResult result = validator.Validate(options);
        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        // Surface every rule failure as a startup error, not just a generic message.
        IEnumerable<string> failures = result.Errors.Select(e =>
            $"{typeof(TOptions).Name}.{e.PropertyName}: {e.ErrorMessage}");

        return ValidateOptionsResult.Fail(failures);
    }
}
