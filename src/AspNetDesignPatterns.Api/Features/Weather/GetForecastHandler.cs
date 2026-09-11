using KAM.Common.Handlers;
using KAM.Common.Pipeline;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Orchestrates the forecast request by composing a <see cref="ForecastContext"/> pipeline
/// (clamp window → fetch → round) and running it, returning its outcome as a
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
            .Use<ClampForecastWindowStep>()
            .Use<FetchForecastStep>()
            .Use<RoundTemperaturesStep>()
            .Build();

        Result result = await pipeline.ExecuteAsync(context, cancellationToken);

        return result.IsFailure
            ? Result.Failure<ForecastResponse>(result.Error)
            : context.Response!;
    }
}
