using FluentValidation;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Query parameters for <c>GET /weather/forecast</c>. Bound via <c>[AsParameters]</c> and
/// checked by <see cref="GetForecastRequestValidator"/> before the handler runs.
/// </summary>
public sealed record GetForecastRequest(double Latitude, double Longitude, int Days = 3);

/// <summary>
/// The single owner of every request rule — including the forecast window, which comes from
/// <see cref="WeatherOptions.MaxForecastDays"/> rather than a hard-coded literal, so a config
/// change and the validation message can't drift apart, and every violation renders as the same
/// 400 <c>ValidationProblemDetails</c> shape.
/// </summary>
internal sealed class GetForecastRequestValidator : AbstractValidator<GetForecastRequest>
{
    public GetForecastRequestValidator(IOptions<WeatherOptions> options)
    {
        int maxDays = options.Value.MaxForecastDays;

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90 degrees.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180 degrees.");

#pragma warning disable MA0076 // a plain int in an English sentence, not a machine-read value
        RuleFor(x => x.Days)
            .InclusiveBetween(1, maxDays)
            .WithMessage($"Days must be between 1 and {maxDays}.");
#pragma warning restore MA0076
    }
}
