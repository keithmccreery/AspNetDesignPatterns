using KAM.Common.Pipeline;

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
