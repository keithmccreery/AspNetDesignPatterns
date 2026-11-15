using KAM.Common.Handlers;
using KAM.Common.Pipeline;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Orchestrates the forecast request by composing a <see cref="ForecastContext"/> pipeline
/// (fetch → round) and running it, returning its outcome as a
/// <see cref="Result{T}"/>.
/// </summary>
internal sealed class GetForecastHandler(IPipelineFactory pipelineFactory)
    : IRequestHandler<GetForecastRequest, ForecastResponse>
{
    public async Task<Result<ForecastResponse>> HandleAsync(
        GetForecastRequest request,
        CancellationToken cancellationToken)
    {
        ForecastContext context = new(request);

        Pipeline<ForecastContext> pipeline = pipelineFactory.CreateBuilder<ForecastContext>()
            .Use<FetchForecastStep>()
            .Use<RoundTemperaturesStep>()
            .Build();

        Result result = await pipeline.ExecuteAsync(context, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error;
        }

        // A step that short-circuits with success (or forgets to set Response) is a bug in this
        // pipeline -- surface it as a 500 rather than a 200 with a null body.
        return context.Response is { } response
            ? response
            : Error.Failure("Weather.NoResponse", "The forecast pipeline completed without producing a response.");
    }
}
