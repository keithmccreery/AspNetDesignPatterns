using FluentValidation;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Query parameters for <c>GET /weather/forecast</c>. Bound via <c>[AsParameters]</c> and
/// checked by <see cref="GetForecastRequestValidator"/> before the handler runs.
/// </summary>
public sealed record GetForecastRequest(double Latitude, double Longitude, int Days = 3);

internal sealed class GetForecastRequestValidator : AbstractValidator<GetForecastRequest>
{
    public GetForecastRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90 degrees.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180 degrees.");

        RuleFor(x => x.Days)
            .InclusiveBetween(1, 16)
            .WithMessage("Days must be between 1 and 16.");
    }
}
