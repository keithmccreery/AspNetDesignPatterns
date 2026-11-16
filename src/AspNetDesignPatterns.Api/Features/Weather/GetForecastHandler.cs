using KAM.Common.DependencyInjection;
using KAM.Common.Pipeline;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Orchestrates the forecast request by composing a <see cref="ForecastContext"/> pipeline
/// (fetch → round) and running it, returning its outcome as a
/// <see cref="Result{T}"/>.
/// </summary>
internal sealed class GetForecastHandler : IRequestHandler<GetForecastRequest, ForecastResponse>
{
    private readonly IPipelineFactory _pipelineFactory;

    public GetForecastHandler(IPipelineFactory pipelineFactory) => _pipelineFactory = pipelineFactory;

    public async Task<Result<ForecastResponse>> HandleAsync(
        GetForecastRequest request,
        CancellationToken cancellationToken)
    {
        ForecastContext context = new(request);

        Pipeline<ForecastContext> pipeline = _pipelineFactory.CreateBuilder<ForecastContext>()
            .Use<FetchForecastStep>()
            .Use<RoundTemperaturesStep>()
            .Build();

        Result result = await pipeline.ExecuteAsync(context, cancellationToken);

        // The pipeline returns a plain Result (steps share ForecastContext, not a return value),
        // so the failure is re-wrapped as a Result<ForecastResponse> and the response is read
        // back off the context.
        if (result.IsFailure)
        {
            return Result.Failure<ForecastResponse>(result.Error);
        }

        // Success with no Response means a step returned success without setting it -- a bug in
        // this pipeline, so a 500, never a 200 with an empty body.
        if (context.Response is null)
        {
            return Result.Failure<ForecastResponse>(WeatherErrors.NoResponse);
        }

        return Result.Success(context.Response);
    }
}
