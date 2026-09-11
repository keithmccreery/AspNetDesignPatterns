using System.Reflection;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace KAM.Common.DependencyInjection;

/// <summary>
/// Base class for a strongly-typed settings object that registers itself: it binds to the
/// configuration section named by its own <c>public static string Section</c> property and
/// validates on startup — with FluentValidation when an <see cref="IValidator{T}"/> is
/// registered for <typeparamref name="T"/>, otherwise with DataAnnotations.
/// </summary>
/// <typeparam name="T">
/// The concrete settings type. Must declare <c>public static string Section</c>.
/// </typeparam>
/// <example>
/// <code>
/// public sealed class WeatherOptions : SettingsBase&lt;WeatherOptions&gt;
/// {
///     public static string Section => "Weather";
///     public string BaseAddress { get; init; } = "https://api.open-meteo.com";
/// }
/// </code>
/// </example>
public abstract class SettingsBase<T> : ISettings
    where T : class
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The <c>Section</c> property is missing, null, or empty.</exception>
    public void RegisterSettings(IServiceCollection services)
    {
        string? section = (string?) typeof(T)
            .GetProperty("Section", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);

        if (string.IsNullOrEmpty(section))
        {
            throw new InvalidOperationException(
                $"{typeof(T).Name} must declare a non-empty 'public static string Section'.");
        }

        OptionsBuilder<T> optionsBuilder = services
            .AddOptions<T>()
            .BindConfiguration(section);

        bool hasFluentValidator = services.Any(d => d.ServiceType == typeof(IValidator<T>));

        if (hasFluentValidator)
        {
            // Resolve and run the FluentValidation validator from the real root provider,
            // surfacing every rule failure as a startup error (not just a generic message).
            services.AddSingleton<IValidateOptions<T>>(sp => new FluentValidateOptions<T>(sp));
        }
        else
        {
            optionsBuilder.ValidateDataAnnotations();
        }

        optionsBuilder.ValidateOnStart();
    }
}
