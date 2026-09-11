using AspNetDesignPatterns.Api.Shared.Pipeline;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Mutable state threaded through the forecast pipeline. Steps read <see cref="Request"/>
/// and progressively populate <see cref="Response"/>.
/// </summary>
internal sealed class ForecastContext(GetForecastRequest request)
{
    public GetForecastRequest Request { get; } = request;

    public ForecastResponse? Response { get; set; }
}

/// <summary>Gate: enforce the configured maximum forecast window before we hit the provider.</summary>
internal sealed class ClampForecastWindowStep(IOptions<WeatherOptions> options)
    : IPipelineStep<ForecastContext>
{
    public Task<Result> ExecuteAsync(
        ForecastContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        if (context.Request.Days > options.Value.MaxForecastDays)
        {
            // Do not call next: the rest of the pipeline is skipped.
#pragma warning disable MA0076 // a plain int in an English sentence, not a machine-read value — unlike the query string in OpenMeteoWeatherClient, culture-invariance buys nothing here
            return Task.FromResult(Result.Failure(Error.Validation(
                "Weather.ForecastWindowTooLarge",
                $"At most {options.Value.MaxForecastDays} days can be requested.")));
#pragma warning restore MA0076
        }

        return next(cancellationToken);
    }
}

/// <summary>Core: call the service and stash the mapped forecast on the context.</summary>
internal sealed class FetchForecastStep(WeatherService service) : IPipelineStep<ForecastContext>
{
    public async Task<Result> ExecuteAsync(
        ForecastContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        Result<ForecastResponse> result = await service.GetForecastAsync(context.Request, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        context.Response = result.Value;
        return await next(cancellationToken);
    }
}

/// <summary>Post-process: runs <em>after</em> the rest of the pipeline, rounding temperatures.</summary>
internal sealed class RoundTemperaturesStep : IPipelineStep<ForecastContext>
{
    public async Task<Result> ExecuteAsync(
        ForecastContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        Result result = await next(cancellationToken);
        if (result.IsFailure || context.Response is not { } response)
        {
            return result;
        }

        List<DailyForecast> rounded = response.Days
            .Select(d => d with
            {
                TemperatureMaxC = Math.Round(d.TemperatureMaxC, 1),
                TemperatureMinC = Math.Round(d.TemperatureMinC, 1),
            })
            .ToList();

        context.Response = response with { Days = rounded };
        return result;
    }
}
