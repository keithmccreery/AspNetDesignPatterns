using AspNetDesignPatterns.Api.DependencyInjection;

using FluentValidation;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Weather feature settings, bound from the "Weather" section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// A <see cref="WeatherOptionsValidator"/> is registered, so <see cref="SettingsBase{T}"/>
/// validates this with FluentValidation — the primary path (contrast with
/// <see cref="Shared.Auth.JwtOptions"/>, which has no validator and falls back to DataAnnotations).
/// </remarks>
public sealed class WeatherOptions : SettingsBase<WeatherOptions>
{
    public static string Section => "Weather";

    /// <summary>Base address of the open-meteo forecast API.</summary>
    public string BaseAddress { get; init; } = "https://api.open-meteo.com";

    /// <summary>Per-request timeout for the typed <see cref="HttpClient"/>.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Upper bound on the forecast window the API will request from upstream.</summary>
    public int MaxForecastDays { get; init; } = 16;
}

internal sealed class WeatherOptionsValidator : AbstractValidator<WeatherOptions>
{
    public WeatherOptionsValidator()
    {
        RuleFor(x => x.BaseAddress)
            .NotEmpty()
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("BaseAddress must be an absolute URI.");

        RuleFor(x => x.RequestTimeout)
            .GreaterThan(TimeSpan.Zero)
            .LessThanOrEqualTo(TimeSpan.FromSeconds(30));

        RuleFor(x => x.MaxForecastDays).InclusiveBetween(1, 16);
    }
}
