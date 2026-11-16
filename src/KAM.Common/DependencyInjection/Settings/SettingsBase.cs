using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KAM.Common.DependencyInjection;

/// <summary>
/// Base class for a strongly-typed settings object that registers itself: it binds to the
/// configuration section named by <see cref="ISettingsSection.Section"/> and validates on
/// startup — with FluentValidation when an <c>IValidator&lt;T&gt;</c> is registered for
/// <typeparamref name="T"/>, otherwise with DataAnnotations.
/// </summary>
/// <typeparam name="T">
/// The concrete settings type itself (the curiously-recurring pattern), which must also
/// implement <see cref="ISettingsSection"/> — so a class that forgets <c>Section</c> doesn't
/// build. Naming a <em>different</em> valid settings type (a copy-paste slip) does compile, so
/// <see cref="RegisterSettings"/> rejects it at startup.
/// </typeparam>
/// <example>
/// FluentValidation (a validator exists, so it's used):
/// <code>
/// public sealed class WeatherOptions : SettingsBase&lt;WeatherOptions&gt;, ISettingsSection
/// {
///     public static string Section => "Weather";
///     public string BaseAddress { get; init; } = "https://api.open-meteo.com";
/// }
///
/// internal sealed class WeatherOptionsValidator : AbstractValidator&lt;WeatherOptions&gt;
/// {
///     public WeatherOptionsValidator() => RuleFor(x => x.BaseAddress).NotEmpty();
/// }
/// </code>
/// DataAnnotations (no validator, so the attributes are used):
/// <code>
/// public sealed class JwtOptions : SettingsBase&lt;JwtOptions&gt;, ISettingsSection
/// {
///     public static string Section => "Jwt";
///
///     [Required, MinLength(32)]
///     public string SigningKey { get; init; } = string.Empty;
/// }
/// </code>
/// </example>
public abstract class SettingsBase<T> : ISettings
    where T : SettingsBase<T>, ISettingsSection
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> isn't this class, or <see cref="ISettingsSection.Section"/> is null or empty.
    /// </exception>
    public void RegisterSettings(IServiceCollection services)
    {
        // `class FooOptions : SettingsBase<BarOptions>` satisfies the constraints whenever
        // BarOptions is itself valid; left alone, it would silently register BarOptions instead.
        if (GetType() != typeof(T))
        {
            throw new InvalidOperationException(
                $"{GetType().Name} derives from SettingsBase<{typeof(T).Name}>; it must name itself: SettingsBase<{GetType().Name}>.");
        }

        // The compiler guarantees Section exists; only its value can still be wrong.
        string section = T.Section;
        if (string.IsNullOrEmpty(section))
        {
            throw new InvalidOperationException($"{typeof(T).Name}.Section must not be empty.");
        }

        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateOnStart();

        // Which validator to use is decided when validation RUNS (ValidateOnStart, after the
        // container is built), not here — so it doesn't matter whether the app registers its
        // FluentValidation validators before or after calling AddSettings().
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<T>, SettingsValidateOptions<T>>());
    }
}
